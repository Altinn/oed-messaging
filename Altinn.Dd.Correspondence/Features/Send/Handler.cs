using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Microsoft.Extensions.Options;

namespace Altinn.Dd.Correspondence.Features.Send;

internal class Handler : IHandler<DdCorrespondenceDetails, CorrespondenceResult>
{
    private const string LanguageCode = "nb";
    private const string SenderReferencePrefix = "EXT_DD_SHIP_";
    private const string CountryCode = "0192";

    private readonly DdCorrespondenceOptions _correspondenceOptions;
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient,
        IOptionsMonitor<DdCorrespondenceOptions> optionsMonitor)
    {
        _httpClient = httpClient;
        _correspondenceOptions = optionsMonitor.CurrentValue;
    }

    public async Task<CorrespondenceResult> Handle(DdCorrespondenceDetails correspondenceDetails)
    {
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
                    IgnoreReservation = correspondenceDetails.IgnoreReservation
                },
                Recipients = [FormatRecipient(correspondenceDetails.Recipient ?? string.Empty)],
                ExistingAttachments = [],
                IdempotentKey = correspondenceDetails.IdempotencyKey
            };

            var result = await _httpClient.CorrespondencePOSTAsync(correspondenceRequest);
            var receipt = new ReceiptExternal(result.ToDto(), correspondenceDetails.IdempotencyKey, sendersReference);
            return CorrespondenceResult.Success(receipt);
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return CorrespondenceResult.Failure(e.Result.Detail);
        }
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
            notification.EmailContentType = (HttpClients.EmailContentType)notificationDetails.EmailContentType;
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
        else if (hasSmsNotification)
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
