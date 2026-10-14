namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// The receipt for a successfully created correspondence.
/// </summary>
/// <param name="InitalizedCorrespondences">What Altinn created for the request.</param>
/// <param name="IdempotencyKey">The idempotency key the correspondence was created under, so a
/// retry can be recognised as a duplicate.</param>
/// <param name="SendersReference">The senders reference the correspondence was created with,
/// either the caller's own or the generated <c>EXT_DD_SHIP_</c> default.</param>
public record ReceiptExternal(InitializedCorrespondences InitalizedCorrespondences, Guid IdempotencyKey, string SendersReference);

/// <summary>
/// The correspondences and attachments created by a single send.
/// </summary>
/// <param name="Correspondences">One entry per recipient the correspondence was created for.</param>
/// <param name="AttachmentIds">The ids of the attachments stored with the correspondence.</param>
public record InitializedCorrespondences(ICollection<InitializedCorrespondence> Correspondences, ICollection<Guid> AttachmentIds);

/// <summary>
/// A correspondence created for one recipient.
/// </summary>
/// <param name="CorrespondenceId">The unique id of the correspondence, used to retrieve it later.</param>
/// <param name="Status">The status the correspondence was created in.</param>
/// <param name="Recipient">The recipient it was created for, in the format Altinn resolved.</param>
/// <param name="Notifications">The notifications requested for this recipient, if any.</param>
public record InitializedCorrespondence(Guid CorrespondenceId, CorrespondenceStatus Status, string Recipient, ICollection<InitializedNotification>? Notifications);

/// <summary>
/// A notification requested alongside a correspondence.
/// </summary>
/// <param name="OrderId">The notification order id, or <c>null</c> if no order was created.</param>
/// <param name="IsReminder">Whether this is the reminder rather than the initial notification.</param>
/// <param name="Status">Whether the notification order was created, and why not if it was not.</param>
public record InitializedNotification(Guid? OrderId, bool? IsReminder, InitializedNotificationStatus Status);
