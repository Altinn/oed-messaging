using System.ServiceModel;
using Altinn.Oed.Messaging.Exceptions;
using Altinn.Oed.Messaging.ExternalServices.Correspondence;
using Altinn.Oed.Messaging.Models;
using Altinn.Oed.Messaging.Models.Interfaces;
using Altinn.Oed.Messaging.Services.Interfaces;
using AltinnFault = Altinn.Oed.Messaging.ExternalServices.Correspondence.AltinnFault;

namespace Altinn.Oed.Messaging.Services;

/// <summary>
/// The <see cref="OedMessagingService"/> class is an implementation of the <see cref="IOedMessagingService"/> interface and represents
/// a wrapper around a client of the Altinn Correspondence service.
/// </summary>
public class OedMessagingService : IOedMessagingService
{
    private const string LanguageCode = "1044";
    private const string FromAddress = "no-reply@altinn.no";
    private const string NotificationType = "TokenTextOnly";

    /// <summary>
    /// Gets or sets the service code of the correspondence service to use when creating correspondence elements.
    /// </summary>
    public string CorrespondenceServiceCode { get; set; }

    /// <summary>
    /// Gets or sets the service edition code of the correspondence service to use when creating correspondence elements.
    /// </summary>
    public string CorrespondenceServiceEdition { get; set; }

    /// <summary>
    /// Gets or sets the system user name to use in authentication with the Correspondence service.
    /// </summary>
    public string SystemUserName { get; set; }

    /// <summary>
    /// Gets or sets the system password to use in authentication with the Correspondence service.
    /// </summary>
    public string SystemPassword { get; set; }

    /// <summary>
    /// Gets or sets the system user code to use when creating a correspondence.
    /// </summary>
    public string SystemUserCode { get; set; }

    private static Random _rand = new Random();

    private static int Rand => _rand.Next(100000, 999999);

    private readonly IChannelManagerService _channelManagerService;

    public OedMessagingService(IChannelManagerService channelManagerService, IOedNotificationSettings settings)
    {
        _channelManagerService = channelManagerService;

        var correspondenceSettings = settings.CorrespondenceSettings.Split(',');
        var correspondenceServiceCode = correspondenceSettings[0].Trim();
        var correspondenceServiceEdition = correspondenceSettings[1].Trim();
        var systemUserCode = correspondenceSettings[2].Trim();

        CorrespondenceServiceCode = correspondenceServiceCode;
        CorrespondenceServiceEdition = correspondenceServiceEdition;

        SystemUserName = settings.AgencySystemUserName;
        SystemPassword = settings.AgencySystemPassword;
        SystemUserCode = systemUserCode;

        //for profiling and bughunting only
        if (settings.UseAltinnTestServers)
        {
            _channelManagerService.Add<CorrespondenceAgencyExternalBasicClient>("ServiceEngineExternal/CorrespondenceAgencyExternalBasic.svc?bigiptestversion=true");
        }
        else
        {
            _channelManagerService.Add<CorrespondenceAgencyExternalBasicClient>("ServiceEngineExternal/CorrespondenceAgencyExternalBasic.svc");
        }
    }

    /// <inheritdoc />        
    public async Task<ReceiptExternal> SendMessage(OedMessageDetails correspondence)
    {
        var insertCorrespondence = new InsertCorrespondenceV2
        {
            ServiceCode = CorrespondenceServiceCode,
            ServiceEdition = CorrespondenceServiceEdition,
            Reportee = correspondence.Recipient,
            MessageSender = correspondence.Sender,
            VisibleDateTime = correspondence.VisibleDateTime ?? DateTime.Now,
            AllowForwarding = correspondence.AllowForwarding,
            IsReservable = false,
            Content = new ExternalContentV2
            {
                LanguageCode = LanguageCode,
                MessageTitle = correspondence.Title,
                MessageSummary = correspondence.Summary,
                MessageBody = correspondence.Body,
                Attachments = new AttachmentsV2()
            },
            Notifications = new NotificationBEList()
        };

        if (correspondence.Notification.SmsText != null)
        {
            insertCorrespondence.Notifications.Add(
                CreateNotification(TransportType.SMS, correspondence.Notification.SmsText, string.Empty, correspondence.ShipmentDatetime));
        }

        if (correspondence.Notification.EmailSubject != null && correspondence.Notification.EmailBody != null)
        {
            insertCorrespondence.Notifications.Add(
                CreateNotification(TransportType.Email, correspondence.Notification.EmailSubject, correspondence.Notification.EmailBody, correspondence.ShipmentDatetime));
        }

        var result = await _channelManagerService.With<CorrespondenceAgencyExternalBasicClient>(async x =>
                await x.InsertCorrespondenceBasicV2Async(SystemUserName, SystemPassword, SystemUserCode, $"EXT_OED_SHIP_{Rand}", insertCorrespondence))
            as InsertCorrespondenceBasicV2Response;

        ReceiptExternal? reply;
        try
        {
            reply = result?.Body.InsertCorrespondenceBasicV2Result;

            if (reply == null)
            {
                throw new Exception("Response was unexpectedly null");
            }

            if (reply.ReceiptStatusCode != ReceiptStatusEnum.OK)
            {
                throw new MessagingServiceException($"{reply.ReceiptStatusCode}: {reply.ReceiptText}");
            }
        }
        catch (FaultException<AltinnFault> e)
        {
            throw new MessagingServiceException($"Could not send correspondence to Altinn: {e.Detail.AltinnErrorMessage}");
        }
        catch (Exception e)
        {
            throw new MessagingServiceException($"Could not send correspondence to Altinn: {e.Message}");
        }


        return reply;
    }

    private static Notification1 CreateNotification(TransportType transportType, string subject, string body, DateTime? shipmentDatetime)
    {
        return new Notification1
        {
            ShipmentDateTime = shipmentDatetime ?? DateTime.Now,
            FromAddress = FromAddress,
            LanguageCode = LanguageCode,
            NotificationType = NotificationType,
            ReceiverEndPoints = new ReceiverEndPointBEList
            {
                new()
                {
                    TransportType = transportType
                }
            },
            TextTokens = new TextTokenSubstitutionBEList
            {
                new()
                {
                    TokenNum = 0,
                    TokenValue = subject
                },
                new()
                {
                    TokenNum = 1,
                    TokenValue = body
                }
            }
        };
    }
}
