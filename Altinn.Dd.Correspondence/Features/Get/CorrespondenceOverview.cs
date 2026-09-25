namespace Altinn.Dd.Correspondence.Features.Get;

/// <summary>
/// An object representing an overview of a correspondence with enough details to drive the business process
/// </summary>
/// <param name="ResourceId">The Resource Id associated with the correspondence service.</param>
/// <param name="SendersReference">A reference used by senders and receivers to identify a specific Correspondence using external identification methods.</param>
/// <param name="MessageSender">An alternative name for the sender of the correspondence. The name, if set, will be displayed instead of the organization name in the inbox.</param>
/// <param name="Content"></param>
/// <param name="RequestedPublishTime">When the correspondence should become visible to the recipient.</param>
/// <param name="AllowSystemDeleteAfter">When Altinn can remove the correspondence from its database.</param>
/// <param name="DueDateTime">When the recipient must reply to the correspondence.</param>
/// <param name="ExternalReferences">A list of references Senders can use to tell the recipient that the correspondence is related to the referenced item(s)
/// <br/>If an external reference of type DialogportenDialogId is set, a transmission will be created on the existing dialog instead.</param>
/// <param name="PropertyList">User-defined properties related to the Correspondence</param>
/// <param name="ReplyOptions">Options for how the recipient can reply to the Correspondence</param>
/// <param name="Notification"></param>
/// <param name="IgnoreReservation">Specifies whether the correspondence can override reservation against digital communication in KRR</param>
/// <param name="Published">Is null until the correspondence is published.</param>
/// <param name="IsConfirmationNeeded">Specifies whether reading the correspondence needs to be confirmed by the recipient</param>
/// <param name="IsConfidential">Specifies whether the correspondence is confidential</param>
/// <param name="Recipient">The recipient of the correspondence.</param>
/// <param name="CorrespondenceId">Unique Id for this correspondence</param>
/// <param name="Created">When the correspondence was created</param>
/// <param name="Status"></param>
/// <param name="StatusText">The current status text for the Correspondence</param>
/// <param name="StatusChanged">Timestamp for when the Current Correspondence Status was changed</param>
/// <param name="Notifications">An overview of the notifications for this correspondence</param>
/// <param name="Altinn2CorrespondenceId">The identifier/reference from Altinn 2 for migrated correspondence. Will be null for correspondence created in Altinn 3.</param>
public record CorrespondenceOverview(
    string ResourceId,
    string SendersReference,
    string? MessageSender,
    CorrespondenceContent? Content,
    DateTimeOffset? RequestedPublishTime,
    DateTimeOffset? AllowSystemDeleteAfter,
    DateTimeOffset? DueDateTime,
    ICollection<ExternalReference>? ExternalReferences,
    IDictionary<string, string>? PropertyList,
    ICollection<CorrespondenceReplyOption>? ReplyOptions,
    InitializeCorrespondenceNotification? Notification,
    bool? IgnoreReservation,
    DateTimeOffset? Published,
    bool IsConfirmationNeeded,
    bool IsConfidential,
    string? Recipient,
    Guid CorrespondenceId,
    DateTimeOffset Created,
    CorrespondenceStatus Status,
    string? StatusText,
    DateTimeOffset StatusChanged,
    ICollection<CorrespondenceNotificationOverview>? Notifications,
    int? Altinn2CorrespondenceId)
{
    /// <summary>
    /// The id of the Dialogporten dialog this correspondence belongs to, taken from its
    /// <see cref="ReferenceType.DialogportenDialogId"/> external reference. Null until Altinn has
    /// created the dialog, which happens after the correspondence is published.
    /// </summary>
    public Guid? DialogId =>
        ExternalReferences?
            .Where(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)
            .Select(reference => Guid.TryParse(reference.ReferenceValue, out var id) ? id : (Guid?)null)
            .FirstOrDefault(id => id is not null);
}

/// <summary>
/// Represents the content of a reportee element of the type correspondence.
/// </summary>
/// <param name="Language">Gets or sets the language of the correspondence, specified according to ISO 639-1</param>
/// <param name="MessageTitle">Gets or sets the correspondence message title. Subject.</param>
/// <param name="MessageSummary">Gets or sets a summary text of the correspondence.</param>
/// <param name="MessageBody">Gets or sets the main body of the correspondence.</param>
/// <param name="Attachments">Gets or sets a list of attachments.</param>
public record CorrespondenceContent(
    string? Language,
    string? MessageTitle,
    string? MessageSummary,
    string? MessageBody,
    ICollection<CorrespondenceAttachment>? Attachments);

/// <summary>
/// Represents a binary attachment to a Correspondence
/// </summary>
/// <param name="FileName">The name of the attachment file.</param>
/// <param name="DisplayName">A logical name for the file, which will be shown in Altinn Inbox.</param>
/// <param name="IsEncrypted">A value indicating whether the attachment is encrypted or not.</param>
/// <param name="Checksum">MD5 checksum for file data.</param>
/// <param name="SendersReference">A reference value given to the attachment by the creator.</param>
/// <param name="Id">A unique id for the correspondence attachment.</param>
/// <param name="DataLocationType"></param>
/// <param name="Created">The date on which this attachment is created</param>
/// <param name="Status"></param>
/// <param name="StatusText">Current attachment status text description</param>
/// <param name="StatusChanged">Timestamp for when the Current Attachment Status was changed</param>
/// <param name="ExpirationTime">When the attachment expires</param>
/// <param name="DataType">The attachment data type in MIME format</param>
public record CorrespondenceAttachment(
    string? FileName,
    string? DisplayName,
    bool IsEncrypted,
    string? Checksum,
    string SendersReference,
    Guid Id,
    AttachmentDataLocationType DataLocationType,
    DateTimeOffset Created,
    AttachmentStatus Status,
    string? StatusText,
    DateTimeOffset StatusChanged,
    DateTimeOffset ExpirationTime,
    string? DataType);

/// <summary>
/// Defines the location of the attachment data
/// </summary>
public enum AttachmentDataLocationType
{
    /// <summary>Stored by Altinn alongside the correspondence.</summary>
    AltinnCorrespondenceAttachment = 0,

    /// <summary>Held in external storage and referenced by the correspondence.</summary>
    ExternalStorage = 1,
}

/// <summary>
/// Represents the important statuses for an attachment
/// </summary>
public enum AttachmentStatus
{
    /// <summary>Registered, but no data uploaded yet.</summary>
    Initialized = 0,

    /// <summary>Upload received and being processed.</summary>
    UploadProcessing = 1,

    /// <summary>Available to the recipient.</summary>
    Published = 2,

    /// <summary>Deleted.</summary>
    Purged = 3,

    /// <summary>Processing failed.</summary>
    Failed = 4,
}

/// <summary>
/// Represents a reference to another item in the Altinn ecosystem
/// </summary>
/// <param name="ReferenceValue">The Reference Value</param>
/// <param name="ReferenceType"></param>
public record ExternalReference(
    string? ReferenceValue,
    ReferenceType ReferenceType);

/// <summary>
/// Defines what kind of reference
/// </summary>
public enum ReferenceType
{
    /// <summary>An arbitrary reference with no defined meaning to Altinn.</summary>
    Generic = 0,

    /// <summary>An instance of an Altinn app.</summary>
    AltinnAppInstance = 1,

    /// <summary>A file transfer in Altinn Broker.</summary>
    AltinnBrokerFileTransfer = 2,

    /// <summary>
    /// A Dialogporten dialog. Setting this makes the correspondence a transmission on the
    /// existing dialog rather than a new one.
    /// </summary>
    DialogportenDialogId = 3,

    /// <summary>A Dialogporten process.</summary>
    DialogportenProcessId = 4,

    /// <summary>A Dialogporten transmission.</summary>
    DialogportenTransmissionId = 5,

    /// <summary>
    /// The Dialogporten transmission type of a correspondence sent to an existing dialog. Only
    /// valid together with a <see cref="DialogportenDialogId"/> reference.
    /// </summary>
    DialogportenTransmissionType = 6,
}

/// <summary>
/// Represents a ReplyOption with information provided by the sender.
/// <br/>A reply option is a way for recipients to respond to a correspondence in addition to the normal Read and Confirm operations
/// </summary>
/// <param name="LinkURL">Gets or sets the URL to be used as a reply/response to a correspondence.</param>
/// <param name="LinkText">Gets or sets the url text.</param>
public record CorrespondenceReplyOption(
    string? LinkURL,
    string? LinkText);

/// <summary>
/// Used to specify a single notification connected to a specific Correspondence during the Initialize Correspondence operation
/// </summary>
/// <param name="NotificationTemplate"></param>
/// <param name="EmailSubject">The emails subject for the main notification</param>
/// <param name="EmailBody">The email body for the main notification</param>
/// <param name="EmailContentType"></param>
/// <param name="SmsBody">The sms body for the main notification</param>
/// <param name="SendReminder">Should a reminder be sent if the notification is not confirmed or opened</param>
/// <param name="ReminderEmailSubject">The email subject to use for the reminder notification</param>
/// <param name="ReminderEmailBody">The email body to use for the reminder notification</param>
/// <param name="ReminderEmailContentType"></param>
/// <param name="ReminderSmsBody">The sms body to use for the reminder notification</param>
/// <param name="NotificationChannel"></param>
/// <param name="ReminderNotificationChannel"></param>
/// <param name="SendersReference">Senders Reference for this notification</param>
/// <param name="RequestedSendTime">The date and time for when the notification should be sent.</param>
/// <param name="CustomRecipients">A list of additional recipients for the notification. These are processed in addition to the Correspondence recipient;
/// <br/>if not set, only the Correspondence recipient receives the notification.</param>
/// <param name="CustomRecipient"></param>
/// <param name="OverrideRegisteredContactInformation">When set to true, only CustomRecipients will be used for notifications, overriding the default correspondence recipient.
/// <br/>This flag can only be used when CustomRecipients is provided.
/// <br/>Default value is false (use default contact info + custom recipients).</param>
public record InitializeCorrespondenceNotification(
    NotificationTemplate NotificationTemplate,
    string? EmailSubject,
    string? EmailBody,
    HttpClients.EmailContentType EmailContentType,
    string? SmsBody,
    bool SendReminder,
    string? ReminderEmailSubject,
    string? ReminderEmailBody,
    HttpClients.EmailContentType ReminderEmailContentType,
    string? ReminderSmsBody,
    NotificationChannel NotificationChannel,
    NotificationChannel ReminderNotificationChannel,
    string? SendersReference,
    DateTimeOffset? RequestedSendTime,
    ICollection<NotificationRecipient>? CustomRecipients,
    NotificationRecipient? CustomRecipient,
    bool OverrideRegisteredContactInformation);

/// <summary>
/// Enum describing available notification templates.
/// </summary>
public enum NotificationTemplate
{
    /// <summary>Use the subject and body supplied on the notification.</summary>
    CustomMessage = 0,

    /// <summary>Use Altinn's standard "you have a new message" wording.</summary>
    GenericAltinnMessage = 1,
}

/// <summary>
/// Enum describing available notification channels.
/// </summary>
public enum NotificationChannel
{
    /// <summary>Email only.</summary>
    Email = 0,

    /// <summary>SMS only.</summary>
    Sms = 1,

    /// <summary>Email where an address is known, otherwise SMS.</summary>
    EmailPreferred = 2,

    /// <summary>SMS where a number is known, otherwise email.</summary>
    SmsPreferred = 3,

    /// <summary>Both email and SMS.</summary>
    EmailAndSms = 4,
}

/// <summary>
/// A class representing a a recipient of a notification
/// </summary>
/// <param name="EmailAddress">the email address of the recipient</param>
/// <param name="MobileNumber">the mobileNumber of the recipient</param>
/// <param name="OrganizationNumber">the organization number of the recipient</param>
/// <param name="NationalIdentityNumber">The SSN of the recipient</param>
/// <param name="IsReserved">Boolean indicating if the recipient is reserved</param>
public record NotificationRecipient(
    string? EmailAddress,
    string? MobileNumber,
    string? OrganizationNumber,
    string? NationalIdentityNumber,
    bool? IsReserved);


/// <summary>
/// A notification that was ordered for a correspondence.
/// </summary>
/// <param name="NotificationOrderId">The notification order id, or <c>null</c> if no order was created.</param>
/// <param name="IsReminder">Whether this is the reminder rather than the initial notification.</param>
public record CorrespondenceNotificationOverview(
    Guid? NotificationOrderId,
    bool IsReminder);

/// <summary>
/// Represents the important statuses for an Correspondence
/// </summary>
public enum CorrespondenceStatus
{
    /// <summary>Created, but not yet processed.</summary>
    Initialized = 0,

    /// <summary>Processed and waiting for its requested publish time.</summary>
    ReadyForPublish = 1,

    /// <summary>Published and visible to the recipient.</summary>
    Published = 2,

    /// <summary>Retrieved by the recipient.</summary>
    Fetched = 3,

    /// <summary>Opened by the recipient.</summary>
    Read = 4,

    /// <summary>The recipient used one of the reply options.</summary>
    Replied = 5,

    /// <summary>The recipient confirmed having read it.</summary>
    Confirmed = 6,

    /// <summary>Deleted by the recipient.</summary>
    PurgedByRecipient = 7,

    /// <summary>Deleted by Altinn, typically once the retention period elapsed.</summary>
    PurgedByAltinn = 8,

    /// <summary>Archived by the recipient.</summary>
    Archived = 9,

    /// <summary>
    /// Not delivered because the recipient is reserved against digital communication in KRR.
    /// Sending with <c>IgnoreReservation</c> overrides this.
    /// </summary>
    Reserved = 10,

    /// <summary>Processing failed.</summary>
    Failed = 11,

    /// <summary>The recipient downloaded the attachments.</summary>
    AttachmentsDownloaded = 12,
}
