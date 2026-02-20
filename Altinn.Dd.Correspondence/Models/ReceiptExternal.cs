namespace Altinn.Dd.Correspondence.Models;

public record ReceiptExternal(InitializedCorrespondences InitalizedCorrespondences, Guid IdempotencyKey, string SendersReference);

public record InitializedCorrespondences(ICollection<InitializedCorrespondence> Correspondences, ICollection<Guid> AttachmentIds);

public record InitializedCorrespondence(Guid CorrespondenceId, CorrespondenceStatus Status, string Recipient, ICollection<InitializedNotification>? Notifications);

public record InitializedNotification(Guid? OrderId, bool? IsReminder, InitializedNotificationStatus Status);