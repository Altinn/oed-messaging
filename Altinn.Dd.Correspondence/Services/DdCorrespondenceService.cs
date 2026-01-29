using Altinn.Dd.Correspondence.Exceptions;
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Microsoft.Extensions.Options;

namespace Altinn.Dd.Correspondence.Services;

public interface IDdCorrespondenceService
{
    /// <summary>
    /// Creates a new correspondence element in Altinn 3.
    /// </summary>
    /// <param name="correspondence">The correspondence details including recipient, content, and notifications.</param>
    /// <returns>A receipt indicating whether the correspondence was successfully created.</returns>
    /// <exception cref="CorrespondenceServiceException">Thrown when the correspondence creation fails.</exception>
    Task<CorrespondenceResult> SendCorrespondence(DdCorrespondenceDetails correspondence);
}

/// <summary>
/// The <see cref="DdCorrespondenceService"/> class is an implementation of the <see cref="IDdCorrespondenceService"/> interface and represents
/// a wrapper around the Altinn 3 Correspondence API client. This service maintains compatibility with the existing Altinn 2 interface
/// while leveraging the modern Altinn 3 REST API for improved performance and reliability.
/// </summary>
public sealed class DdCorrespondenceService : IDdCorrespondenceService
{
    private const string LanguageCode = "nb"; // Norwegian Bokmål for Altinn 3
    private const string SenderReferencePrefix = "EXT_DD_SHIP_";
    private const string CountryCode = "0192";
    
    private readonly DdCorrespondenceOptions _correspondenceOptions;
    private readonly AltinnCorrespondenceClient _correspondenceClient;

    public DdCorrespondenceService(
        HttpClient httpClient,
        IOptionsMonitor<DdCorrespondenceOptions> optionsMonitor)
    {
        _correspondenceClient = new AltinnCorrespondenceClient(httpClient)
        {
            BaseUrl = httpClient.BaseAddress!.ToString()
        };
        _correspondenceOptions = optionsMonitor.CurrentValue;
    }

    /// <inheritdoc />        
    public async Task<CorrespondenceResult> SendCorrespondence(DdCorrespondenceDetails correspondence)
    {
        var sendersReference = correspondence.SendersReference ?? $"{SenderReferencePrefix}{correspondence.IdempotencyKey}";
        try
        {
            var correspondenceRequest = new InitializeCorrespondencesExt
            {
                Correspondence = new BaseCorrespondenceExt
                {
                    ResourceId = _correspondenceOptions.ResourceId,
                    SendersReference = sendersReference,
                    MessageSender = correspondence.Sender,
                    Content = new InitializeCorrespondenceContentExt
                    {
                        Language = LanguageCode,
                        MessageTitle = correspondence.Title,
                        MessageSummary = correspondence.Summary,
                        MessageBody = correspondence.Body,
                        Attachments = []
                    },
                    RequestedPublishTime = correspondence.VisibleDateTime ?? DateTimeOffset.Now,
                    PropertyList = new Dictionary<string, string>(),
                    Notification = CreateNotification(correspondence.Notification, correspondence.ShipmentDatetime),
                    IgnoreReservation = correspondence.IgnoreReservation
                },
                Recipients = [FormatRecipient(correspondence.Recipient ?? string.Empty)],
                ExistingAttachments = [],
                IdempotentKey = correspondence.IdempotencyKey
            };

            var result = await _correspondenceClient.CorrespondencePOSTAsync(correspondenceRequest);
            var receipt = new ReceiptExternal(result.ToDto(), correspondence.IdempotencyKey, sendersReference);
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
