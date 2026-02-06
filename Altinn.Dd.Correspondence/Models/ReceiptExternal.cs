namespace Altinn.Dd.Correspondence.Models;

public record ReceiptExternal(InitializedCorrespondences InitalizedCorrespondences, Guid IdempotencyKey, string SendersReference);

public record InitializedCorrespondences(ICollection<InitializedCorrespondence> Correspondences, ICollection<Guid> AttachmentIds);

public record InitializedCorrespondence(Guid CorrespondenceId, CorrespondenceStatus Status, string Recipient, ICollection<InitializedNotification>? Notifications);

public record InitializedNotification(Guid? OrderId, bool? IsReminder, InitializedNotificationStatus Status);

public enum InitializedNotificationStatus
{
    Success = 0,
    MissingContact = 1,
    Failure = 2,
};

public enum CorrespondenceStatus
{
    Initialized = 0,
    ReadyForPublish = 1,
    Published = 2,
    Fetched = 3,
    Read = 4,
    Replied = 5,
    Confirmed = 6,
    PurgedByRecipient = 7,
    PurgedByAltinn = 8,
    Archived = 9,
    Reserved = 10,
    Failed = 11,
    AttachmentsDownloaded = 12,
};