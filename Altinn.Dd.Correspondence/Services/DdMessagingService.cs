using System.Net.Http;
using Altinn.Dd.Correspondence.Exceptions;
using Altinn.Dd.Correspondence.ExternalServices.Correspondence;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Polly;
using Polly.Retry;

namespace Altinn.Dd.Correspondence.Services;

/// <summary>
/// The <see cref="DdMessagingService"/> class is an implementation of the <see cref="IDdMessagingService"/> interface and represents
/// a wrapper around the Altinn 3 Correspondence API client. This service maintains compatibility with the existing Altinn 2 interface
/// while leveraging the modern Altinn 3 REST API for improved performance and reliability.
/// </summary>
public class DdMessagingService : IDdMessagingService
{
    private const string LanguageCode = "nb"; // Norwegian Bokmål for Altinn 3
    private const string NotificationTemplate = "CustomMessage";
    private const string SenderReferencePrefix = "EXT_DD_SHIP_";

    /// <summary>
    /// Gets or sets the resource ID for the correspondence service.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the sender organization identifier.
    /// </summary>
    public string Sender { get; set; }

    /// <summary>
    /// Gets or sets the country code for organization number formatting.
    /// </summary>
    public string CountryCode { get; set; }

    private static int Rand => Random.Shared.Next(100000, 999999);

    private readonly AltinnCorrespondenceClient _correspondenceClient;
    private readonly ILogger<DdMessagingService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public DdMessagingService(HttpClient httpClient, IDdNotificationSettings settings, ILogger<DdMessagingService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        
        // Validate settings using data annotations
        var validationContext = new ValidationContext(settings);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(settings, validationContext, validationResults, true))
        {
            var errorMessages = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Settings validation failed: {errorMessages}", nameof(settings));
        }
        
        _correspondenceClient = new AltinnCorrespondenceClient(httpClient);
        _logger = logger;
        
        _retryPolicy = Policy
            .Handle<AltinnCorrespondenceException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}s due to: {ExceptionMessage}",
                        retryCount,
                        timespan.TotalSeconds,
                        exception.Message);
                });

        var correspondenceSettings = settings.CorrespondenceSettings.Split(',');
        if (correspondenceSettings.Length < 2)
        {
            throw new ArgumentException("CorrespondenceSettings must contain resourceId and sender separated by comma", nameof(settings));
        }
        
        var resourceId = correspondenceSettings[0].Trim();
        var sender = correspondenceSettings[1].Trim();

        ResourceId = resourceId;
        Sender = sender;
        CountryCode = settings.CountryCode;

        // Set base URL based on test server setting
        if (settings.UseAltinnTestServers)
        {
            _correspondenceClient.BaseUrl = "https://platform.tt02.altinn.no";
        }
        else
        {
            _correspondenceClient.BaseUrl = "https://platform.altinn.no";
        }
    }

    /// <inheritdoc />        
    public async Task<ReceiptExternal> SendMessage(DdMessageDetails correspondence)
    {
        _logger.LogInformation("Starting to send correspondence to recipient {Recipient} with title {Title}", 
            correspondence.Recipient, correspondence.Title);
            
        try
        {
            // Generate idempotency key if not provided
            var idempotencyKey = correspondence.IdempotencyKey ?? Guid.NewGuid();
            
            _logger.LogDebug("Using idempotency key {IdempotencyKey} for correspondence to recipient {Recipient}", 
                idempotencyKey, correspondence.Recipient);

            // Create the correspondence request for Altinn 3
            var correspondenceRequest = new InitializeCorrespondencesExt
            {
                Correspondence = new BaseCorrespondenceExt
                {
                    ResourceId = ResourceId,
                    SendersReference = $"{SenderReferencePrefix}{Rand}",
                    MessageSender = correspondence.Sender,
                    Content = new InitializeCorrespondenceContentExt
                    {
                        Language = LanguageCode,
                        MessageTitle = correspondence.Title,
                        MessageSummary = correspondence.Summary,
                        MessageBody = correspondence.Body,
                        Attachments = new List<InitializeCorrespondenceAttachmentExt>()
                    },
                    RequestedPublishTime = correspondence.VisibleDateTime ?? DateTimeOffset.Now,
                    AllowSystemDeleteAfter = correspondence.VisibleDateTime?.AddYears(1) ?? DateTimeOffset.Now.AddYears(1),
                    PropertyList = new Dictionary<string, string>(),
                    Notification = CreateNotification(correspondence.Notification, correspondence.ShipmentDatetime)
                },
                Recipients = new List<string> { FormatRecipient(correspondence.Recipient ?? string.Empty) },
                ExistingAttachments = new List<Guid>(),
                IdempotentKey = idempotencyKey
            };

            // Send the correspondence using Altinn 3 API with retry policy
            var result = await _retryPolicy.ExecuteAsync(async () =>
                await _correspondenceClient.CorrespondencePOSTAsync(correspondenceRequest));

            _logger.LogInformation("Successfully sent correspondence to recipient {Recipient}", correspondence.Recipient);
            
            // Convert Altinn 3 response to Altinn 2 compatible response
            return ReceiptExternal.CreateSuccess();
        }
        catch (AltinnCorrespondenceException e)
        {
            _logger.LogError(e, "Failed to send correspondence to Altinn 3 for recipient {Recipient}: {ErrorMessage}", 
                correspondence.Recipient, e.Message);
            throw new CorrespondenceServiceException($"Could not send correspondence to Altinn 3: {e.Message}", e);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error while sending correspondence to recipient {Recipient}: {ErrorMessage}", 
                correspondence.Recipient, e.Message);
            throw new CorrespondenceServiceException($"Could not send correspondence to Altinn 3: {e.Message}", e);
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
            notification.EmailContentType = EmailContentType.Plain;
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
    private string FormatRecipient(string recipient)
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
        if (recipient.Contains(":"))
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
