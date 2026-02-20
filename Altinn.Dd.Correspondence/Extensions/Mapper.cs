using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Extensions;

internal static class Mapper
{
    public static InitializedCorrespondences ToDto(this InitializeCorrespondencesResponseExt e)
    {
        return new InitializedCorrespondences(
            Correspondences: [.. e.Correspondences.Select(ToDto)],
            AttachmentIds: e.AttachmentIds
        );
    }

    public static InitializedCorrespondence ToDto(this InitializedCorrespondencesExt e)
    {
        return new InitializedCorrespondence(
            CorrespondenceId: e.CorrespondenceId,
            Status: (CorrespondenceStatus)e.Status,
            Recipient: e.Recipient,
            Notifications: e.Notifications?.Select(ToDto)?.ToList()
        );
    }

    public static InitializedNotification ToDto(this InitializedCorrespondencesNotificationsExt e)
    {
        return new InitializedNotification(
            OrderId: e.OrderId,
            IsReminder: e.IsReminder,
            Status: (InitializedNotificationStatus)e.Status);
    }

    public static Features.Get.CorrespondenceOverview ToDto(this CorrespondenceOverviewExt e)
    {
        return new Features.Get.CorrespondenceOverview(
            ResourceId: e.ResourceId,
            SendersReference: e.SendersReference,
            MessageSender: e.MessageSender,
            Content: e.Content?.ToDto(),
            RequestedPublishTime: e.RequestedPublishTime,
            AllowSystemDeleteAfter: e.AllowSystemDeleteAfter,
            DueDateTime: e.DueDateTime,
            ExternalReferences: e.ExternalReferences?.Select(ToDto)?.ToList(),
            PropertyList: e.PropertyList,
            ReplyOptions: e.ReplyOptions?.Select(ToDto)?.ToList(),
            Notification: e.Notification?.ToDto(),
            IgnoreReservation: e.IgnoreReservation,
            Published: e.Published,
            IsConfirmationNeeded: e.IsConfirmationNeeded,
            IsConfidential: e.IsConfidential,
            Recipient: e.Recipient,
            CorrespondenceId: e.CorrespondenceId,
            Created: e.Created,
            Status: (Features.Get.CorrespondenceStatus)e.Status,
            StatusText: e.StatusText,
            StatusChanged: e.StatusChanged,
            Notifications: e.Notifications?.Select(ToDto)?.ToList(),
            Altinn2CorrespondenceId: e.Altinn2CorrespondenceId);
    }

    public static Features.Get.CorrespondenceContent ToDto(this CorrespondenceContentExt e)
    {
        return new Features.Get.CorrespondenceContent(
            Language: e.Language,
            MessageTitle: e.MessageTitle,
            MessageSummary: e.MessageSummary,
            MessageBody: e.MessageBody,
            Attachments: e.Attachments?.Select(ToDto)?.ToList());
    }

    public static Features.Get.CorrespondenceAttachment ToDto(this CorrespondenceAttachmentExt e)
    {
        return new Features.Get.CorrespondenceAttachment(
            FileName: e.FileName,
            DisplayName: e.DisplayName,
            IsEncrypted: e.IsEncrypted,
            Checksum: e.Checksum,
            SendersReference: e.SendersReference,
            Id: e.Id,
            DataLocationType: (Features.Get.AttachmentDataLocationType)e.DataLocationType,
            Created: e.Created,
            Status: (Features.Get.AttachmentStatus)e.Status,
            StatusText: e.StatusText,
            StatusChanged: e.StatusChanged,
            ExpirationTime: e.ExpirationTime,
            DataType: e.DataType);
    }

    public static Features.Get.ExternalReference ToDto(this ExternalReferenceExt e)
    {
        return new Features.Get.ExternalReference(
            ReferenceValue: e.ReferenceValue,
            ReferenceType: (Features.Get.ReferenceType)e.ReferenceType);
    }

    public static Features.Get.CorrespondenceReplyOption ToDto(this CorrespondenceReplyOptionExt e)
    {
        return new Features.Get.CorrespondenceReplyOption(
            LinkURL: e.LinkURL,
            LinkText: e.LinkText);
    }

    public static Features.Get.InitializeCorrespondenceNotification ToDto(this InitializeCorrespondenceNotificationExt e)
    {
        return new Features.Get.InitializeCorrespondenceNotification(
            NotificationTemplate: (Features.Get.NotificationTemplate)e.NotificationTemplate,
            EmailSubject: e.EmailSubject,
            EmailBody: e.EmailBody,
            EmailContentType: (Features.Get.EmailContentType)e.EmailContentType,
            SmsBody: e.SmsBody,
            SendReminder: e.SendReminder,
            ReminderEmailSubject: e.ReminderEmailSubject,
            ReminderEmailBody: e.ReminderEmailBody,
            ReminderEmailContentType: (Features.Get.EmailContentType)e.ReminderEmailContentType,
            ReminderSmsBody: e.ReminderSmsBody,
            NotificationChannel: (Features.Get.NotificationChannel)e.NotificationChannel,
            ReminderNotificationChannel: (Features.Get.NotificationChannel)e.ReminderNotificationChannel,
            SendersReference: e.SendersReference,
            RequestedSendTime: e.RequestedSendTime,
            CustomRecipients: e.CustomRecipients?.Select(ToDto)?.ToList(),
            CustomRecipient: e.CustomRecipient?.ToDto(),
            OverrideRegisteredContactInformation: e.OverrideRegisteredContactInformation);
    }
    
    public static Features.Get.NotificationRecipient ToDto(this NotificationRecipientExt e)
    {
        return new Features.Get.NotificationRecipient(
            EmailAddress: e.EmailAddress,
            MobileNumber: e.MobileNumber,
            OrganizationNumber: e.OrganizationNumber,
            NationalIdentityNumber: e.NationalIdentityNumber,
            IsReserved: e.IsReserved);
    }

    public static Features.Get.CorrespondenceNotificationOverview ToDto(this HttpClients.CorrespondenceNotificationOverview e)
    {
        return new Features.Get.CorrespondenceNotificationOverview(
            NotificationOrderId: e.NotificationOrderId,
            IsReminder: e.IsReminder);
    }
}
