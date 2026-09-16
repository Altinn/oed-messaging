using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;

using CorrespondenceGet = Altinn.Dd.Correspondence.Features.Get;
using CorrespondenceModels = Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Tests.Extensions;

/// <summary>
/// The mapper translates the generated wire contract into the public DTOs, including a pile of
/// unchecked enum casts. These tests pin the cast pairs that would silently drift if either side
/// were renumbered - a regenerated client is the likely cause.
/// </summary>
public class MapperTests
{
    [Fact]
    public void ToDto_MapsTheInitializeResponse()
    {
        var correspondenceId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var response = new InitializeCorrespondencesResponseExt
        {
            AttachmentIds = [attachmentId],
            Correspondences =
            [
                new InitializedCorrespondencesExt
                {
                    CorrespondenceId = correspondenceId,
                    Recipient = "0192:987654321",
                    Status = CorrespondenceStatusExt.Published,
                    Notifications =
                    [
                        new InitializedCorrespondencesNotificationsExt
                        {
                            OrderId = orderId,
                            IsReminder = true,
                            Status = InitializedNotificationStatusExt.Success
                        }
                    ]
                }
            ]
        };

        var dto = response.ToDto();

        Assert.Equal([attachmentId], dto.AttachmentIds);
        var correspondence = Assert.Single(dto.Correspondences);
        Assert.Equal(correspondenceId, correspondence.CorrespondenceId);
        Assert.Equal("0192:987654321", correspondence.Recipient);
        Assert.Equal(CorrespondenceModels.CorrespondenceStatus.Published, correspondence.Status);

        var notification = Assert.Single(correspondence.Notifications!);
        Assert.Equal(orderId, notification.OrderId);
        Assert.True(notification.IsReminder);
        Assert.Equal(CorrespondenceModels.InitializedNotificationStatus.Success, notification.Status);
    }

    [Fact]
    public void ToDto_MapsTheCorrespondenceOverviewIncludingNestedContent()
    {
        var correspondenceId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow;

        var overview = new CorrespondenceOverviewExt
        {
            ResourceId = "oed-correspondence",
            SendersReference = "caller-reference",
            MessageSender = "Sender",
            CorrespondenceId = correspondenceId,
            Created = created,
            StatusChanged = created,
            Status = CorrespondenceStatusExt.Read,
            StatusText = "Read",
            Recipient = "0192:987654321",
            IgnoreReservation = true,
            IsConfidential = true,
            IsConfirmationNeeded = true,
            Altinn2CorrespondenceId = 42,
            PropertyList = new Dictionary<string, string> { ["key"] = "value" },
            Content = new CorrespondenceContentExt
            {
                Language = "nb",
                MessageTitle = "Title",
                MessageSummary = "Summary",
                MessageBody = "Body",
                Attachments =
                [
                    new CorrespondenceAttachmentExt
                    {
                        Id = attachmentId,
                        FileName = "file.pdf",
                        DisplayName = "File",
                        SendersReference = "attachment-reference",
                        DataType = "application/pdf",
                        DataLocationType = AttachmentDataLocationTypeExt.ExternalStorage,
                        Status = AttachmentStatusExt.Published
                    }
                ]
            },
            ExternalReferences =
            [
                new ExternalReferenceExt
                {
                    ReferenceValue = "dialog-id",
                    ReferenceType = ReferenceTypeExt.DialogportenDialogId
                }
            ],
            ReplyOptions =
            [
                new CorrespondenceReplyOptionExt { LinkURL = "https://example.test", LinkText = "Reply" }
            ]
        };

        var dto = overview.ToDto();

        Assert.Equal("oed-correspondence", dto.ResourceId);
        Assert.Equal(correspondenceId, dto.CorrespondenceId);
        Assert.Equal(CorrespondenceGet.CorrespondenceStatus.Read, dto.Status);
        Assert.True(dto.IgnoreReservation);
        Assert.Equal(42, dto.Altinn2CorrespondenceId);
        Assert.Equal("value", dto.PropertyList!["key"]);

        Assert.Equal("Title", dto.Content!.MessageTitle);
        var attachment = Assert.Single(dto.Content.Attachments!);
        Assert.Equal(attachmentId, attachment.Id);
        Assert.Equal("file.pdf", attachment.FileName);
        Assert.Equal(CorrespondenceGet.AttachmentDataLocationType.ExternalStorage, attachment.DataLocationType);
        Assert.Equal(CorrespondenceGet.AttachmentStatus.Published, attachment.Status);

        var reference = Assert.Single(dto.ExternalReferences!);
        Assert.Equal(CorrespondenceGet.ReferenceType.DialogportenDialogId, reference.ReferenceType);

        var replyOption = Assert.Single(dto.ReplyOptions!);
        Assert.Equal("Reply", replyOption.LinkText);
    }

    [Fact]
    public void ToDto_MapsTheNotificationOnAnOverview()
    {
        var overview = new CorrespondenceOverviewExt
        {
            ResourceId = "oed-correspondence",
            SendersReference = "caller-reference",
            Notification = new InitializeCorrespondenceNotificationExt
            {
                NotificationTemplate = NotificationTemplateExt.CustomMessage,
                NotificationChannel = NotificationChannelExt.EmailAndSms,
                ReminderNotificationChannel = NotificationChannelExt.SmsPreferred,
                EmailContentType = EmailContentType.Html,
                ReminderEmailContentType = EmailContentType.Plain,
                EmailSubject = "Subject",
                EmailBody = "Body",
                SmsBody = "Sms",
                SendReminder = true,
                CustomRecipient = new NotificationRecipientExt
                {
                    EmailAddress = "someone@example.test",
                    IsReserved = false
                }
            }
        };

        var notification = overview.ToDto().Notification;

        Assert.NotNull(notification);
        Assert.Equal(CorrespondenceGet.NotificationTemplate.CustomMessage, notification!.NotificationTemplate);
        Assert.Equal(CorrespondenceGet.NotificationChannel.EmailAndSms, notification.NotificationChannel);
        Assert.Equal(CorrespondenceGet.NotificationChannel.SmsPreferred, notification.ReminderNotificationChannel);
        Assert.Equal(CorrespondenceGet.EmailContentType.Html, notification.EmailContentType);
        Assert.Equal(CorrespondenceGet.EmailContentType.Plain, notification.ReminderEmailContentType);
        Assert.True(notification.SendReminder);
        Assert.Equal("someone@example.test", notification.CustomRecipient!.EmailAddress);
    }

    [Fact]
    public void ToDto_TolerantOfTheOptionalCollectionsBeingAbsent()
    {
        var overview = new CorrespondenceOverviewExt
        {
            ResourceId = "oed-correspondence",
            SendersReference = "caller-reference"
        };

        var dto = overview.ToDto();

        Assert.Null(dto.Content);
        Assert.Null(dto.Notification);
        Assert.Null(dto.ExternalReferences);
        Assert.Null(dto.ReplyOptions);
        Assert.Null(dto.Notifications);
    }
}
