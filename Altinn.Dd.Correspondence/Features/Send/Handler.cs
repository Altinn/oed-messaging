using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Microsoft.Extensions.Options;

namespace Altinn.Dd.Correspondence.Features.Send;

internal class Handler(
    AltinnCorrespondenceClient httpClient,
    IOptionsMonitor<DdCorrespondenceOptions> optionsMonitor) : IHandler<DdCorrespondenceDetails, Result<ReceiptExternal>>
{
    private const string LanguageCode = "nb";
    private const string SenderReferencePrefix = "EXT_DD_SHIP_";
    private const string CountryCode = "0192";

    internal const string TransmissionTypeWithoutDialogId =
        "A TransmissionType can only be set together with a DialogId.";

    private readonly DdCorrespondenceOptions _correspondenceOptions = optionsMonitor.CurrentValue;

    public async Task<Result<ReceiptExternal>> Handle(DdCorrespondenceDetails correspondenceDetails)
    {
        // Altinn rejects this too, but only after a round trip.
        if (correspondenceDetails.TransmissionType is not null && correspondenceDetails.DialogId is null)
        {
            return Result<ReceiptExternal>.Failure(TransmissionTypeWithoutDialogId);
        }

        var sendersReference = correspondenceDetails.SendersReference ?? $"{SenderReferencePrefix}{correspondenceDetails.IdempotencyKey}";
        try
        {
            var correspondenceRequest = new InitializeCorrespondencesExt
            {
                Correspondence = new BaseCorrespondenceExt
                {
                    ResourceId = _correspondenceOptions.ResourceId,
                    SendersReference = sendersReference,
                    MessageSender = correspondenceDetails.Sender,
                    Content = new InitializeCorrespondenceContentExt
                    {
                        Language = LanguageCode,
                        MessageTitle = correspondenceDetails.Title,
                        MessageSummary = correspondenceDetails.Summary,
                        MessageBody = correspondenceDetails.Body,
                        Attachments = []
                    },
                    RequestedPublishTime = correspondenceDetails.VisibleDateTime ?? DateTimeOffset.Now,
                    PropertyList = new Dictionary<string, string>(),
                    Notification = CreateNotification(correspondenceDetails.Notification, correspondenceDetails.ShipmentDatetime),
                    IgnoreReservation = correspondenceDetails.IgnoreReservation,
                    ExternalReferences = CreateExternalReferences(correspondenceDetails)
                },
                Recipients = [FormatRecipient(correspondenceDetails.Recipient ?? string.Empty)],
                ExistingAttachments = [],
                IdempotentKey = correspondenceDetails.IdempotencyKey
            };

            var result = await httpClient.CorrespondencePOSTAsync(correspondenceRequest);
            var receipt = new ReceiptExternal(result.ToDto(), correspondenceDetails.IdempotencyKey, sendersReference);
            return Result<ReceiptExternal>.Success(receipt);
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return Result<ReceiptExternal>.Failure(e.Result.Detail);
        }
        catch (AltinnCorrespondenceException e) when (e.StatusCode == (int)System.Net.HttpStatusCode.Conflict)
        {
            // 409 means Altinn already holds a correspondence under this idempotency key: either a
            // retried attempt whose first try reached Altinn but timed out on our side, or a caller
            // resending. Both are the same send, so answer with the receipt of the one that exists.
            // If it cannot be found, the conflict surfaces as before.
            var existing = await FindExisting(correspondenceDetails.IdempotencyKey);
            if (existing is null)
            {
                throw;
            }

            return Result<ReceiptExternal>.Success(existing);
        }
        catch (Polly.ExecutionRejectedException e)
        {
            throw Exceptions.ResilienceFailure.Translate(e);
        }
    }

    private async Task<ReceiptExternal?> FindExisting(Guid idempotencyKey)
    {
        try
        {
            var search = await httpClient.CorrespondenceGETAsync(
                resourceId: _correspondenceOptions.ResourceId,
                from: null,
                to: null,
                status: null,
                role: CorrespondencesRoleType.Sender,
                onBehalfOf: null,
                sendersReference: null,
                idempotentKey: idempotencyKey);
            if (search.Ids is not { Count: > 0 } ids)
            {
                return null;
            }

            var overviews = new List<CorrespondenceOverviewExt>();
            foreach (var id in ids)
            {
                overviews.Add(await httpClient.CorrespondenceGET2Async(id));
            }

            // The notification orders are not part of the overview, so a recovered receipt has none.
            var correspondences = overviews
                .Select(o => new InitializedCorrespondence(o.CorrespondenceId, (CorrespondenceStatus)o.Status, o.Recipient, Notifications: null))
                .ToList();
            return new ReceiptExternal(
                new InitializedCorrespondences(correspondences, []),
                idempotencyKey,
                overviews[0].SendersReference);
        }
        // Whatever goes wrong with the lookup, the caller is better served by the original conflict.
        catch (AltinnCorrespondenceException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (Polly.ExecutionRejectedException e)
        {
            throw Exceptions.ResilienceFailure.Translate(e);
        }
    }

    private static List<ExternalReferenceExt>? CreateExternalReferences(DdCorrespondenceDetails correspondenceDetails)
    {
        // Without a dialog there is nothing to reference, and the request stays as it was.
        if (correspondenceDetails.DialogId is not { } dialogId)
        {
            return null;
        }

        var references = new List<ExternalReferenceExt>
        {
            new()
            {
                ReferenceType = ReferenceTypeExt.DialogportenDialogId,
                ReferenceValue = dialogId.ToString()
            }
        };

        // Altinn accepts the transmission type by name or by number; send the name.
        if (correspondenceDetails.TransmissionType is { } transmissionType)
        {
            references.Add(new ExternalReferenceExt
            {
                ReferenceType = ReferenceTypeExt.DialogportenTransmissionType,
                ReferenceValue = transmissionType.ToString()
            });
        }

        return references;
    }

    private static InitializeCorrespondenceNotificationExt? CreateNotification(NotificationDetails? notificationDetails, DateTime? shipmentDatetime)
    {
        // If no notification details are provided, return null (no notification)
        if (notificationDetails == null)
        {
            return null;
        }

        // Check if any notification details are provided
        bool hasEmailNotification = !string.IsNullOrEmpty(notificationDetails.EmailSubject) && !string.IsNullOrEmpty(notificationDetails.EmailBody);
        bool hasSmsNotification = !string.IsNullOrEmpty(notificationDetails.SmsText);

        // If no notification details are provided, return null (no notification)
        if (!hasEmailNotification && !hasSmsNotification)
        {
            return null;
        }

        var notification = new InitializeCorrespondenceNotificationExt
        {
            NotificationTemplate = NotificationTemplateExt.CustomMessage,
            RequestedSendTime = shipmentDatetime ?? DateTimeOffset.Now
        };

        // Set email notification if provided
        if (hasEmailNotification)
        {
            notification.EmailSubject = notificationDetails.EmailSubject;
            notification.EmailBody = notificationDetails.EmailBody;
            notification.EmailContentType = notificationDetails.EmailContentType;
        }

        // Set SMS notification if provided
        if (hasSmsNotification)
        {
            notification.SmsBody = notificationDetails.SmsText;
        }

        // Determine notification channel based on what's provided
        if (hasEmailNotification && hasSmsNotification)
        {
            notification.NotificationChannel = NotificationChannelExt.EmailAndSms;
        }
        else if (hasEmailNotification)
        {
            notification.NotificationChannel = NotificationChannelExt.Email;
        }
        else
        {
            notification.NotificationChannel = NotificationChannelExt.Sms;
        }

        return notification;
    }

    /// <summary>
    /// Formats a recipient identifier into the proper format required by Altinn 3.
    /// Supports organization numbers in both URN and country code formats.
    /// </summary>
    /// <param name="recipient">The recipient identifier (organization number)</param>
    /// <returns>The formatted recipient string</returns>
    private static string FormatRecipient(string recipient)
    {
        if (string.IsNullOrEmpty(recipient))
        {
            return string.Empty;
        }

        // Check if it's already in URN format
        if (recipient.StartsWith("urn:altinn:", StringComparison.OrdinalIgnoreCase))
        {
            return recipient;
        }

        // Check if it's already in countrycode:organizationnumber format
        if (recipient.Contains(':'))
        {
            return recipient;
        }

        // For Norwegian organization numbers (9 digits), use country code format
        if (recipient.Length == 9 && recipient.All(char.IsDigit))
        {
            return $"{CountryCode}:{recipient}";
        }

        // If we can't determine the format, return as-is (might cause validation error)
        return recipient;
    }
}
