using System.Net.Http;
using Altinn.Oed.Correspondence.Exceptions;
using Altinn.Oed.Correspondence.ExternalServices.Correspondence;
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services.Interfaces;

namespace Altinn.Oed.Correspondence.Services;

/// <summary>
/// The <see cref="OedMessagingService"/> class is an implementation of the <see cref="IOedMessagingService"/> interface and represents
/// a wrapper around the Altinn 3 Correspondence API client. This service maintains compatibility with the existing Altinn 2 interface
/// while leveraging the modern Altinn 3 REST API for improved performance and reliability.
/// </summary>
public class OedMessagingService : IOedMessagingService
{
    private const string LanguageCode = "nb"; // Norwegian Bokmål for Altinn 3
    private const string NotificationTemplate = "CustomMessage";

    /// <summary>
    /// Gets or sets the resource ID for the correspondence service.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the sender organization identifier.
    /// </summary>
    public string Sender { get; set; }

    private static Random _rand = new Random();

    private static int Rand => _rand.Next(100000, 999999);

    private readonly AltinnCorrespondenceClient _correspondenceClient;

    public OedMessagingService(HttpClient httpClient, IOedNotificationSettings settings)
    {
        _correspondenceClient = new AltinnCorrespondenceClient(httpClient);

        var correspondenceSettings = settings.CorrespondenceSettings.Split(',');
        var resourceId = correspondenceSettings[0].Trim();
        var sender = correspondenceSettings[1].Trim();

        ResourceId = resourceId;
        Sender = sender;

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
    public async Task<ReceiptExternal> SendMessage(OedMessageDetails correspondence)
    {
        try
        {
            // Create the correspondence request for Altinn 3
            var correspondenceRequest = new InitializeCorrespondencesExt
            {
                Correspondence = new BaseCorrespondenceExt
                {
                    ResourceId = ResourceId,
                    SendersReference = $"EXT_OED_SHIP_{Rand}",
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
                    Notification = CreateNotification(correspondence.Notification, correspondence.ShipmentDatetime)
                },
                Recipients = new List<string> { correspondence.Recipient ?? string.Empty }
            };

            // Send the correspondence using Altinn 3 API
            var result = await _correspondenceClient.CorrespondencePOSTAsync(correspondenceRequest);

            // Convert Altinn 3 response to Altinn 2 compatible response
            return ReceiptExternal.CreateSuccess();
        }
        catch (AltinnCorrespondenceException e)
        {
            throw new CorrespondenceServiceException($"Could not send correspondence to Altinn 3: {e.Message}");
        }
        catch (Exception e)
        {
            throw new CorrespondenceServiceException($"Could not send correspondence to Altinn 3: {e.Message}");
        }
    }

    private static InitializeCorrespondenceNotificationExt? CreateNotification(NotificationDetails notificationDetails, DateTime? shipmentDatetime)
    {
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
}
