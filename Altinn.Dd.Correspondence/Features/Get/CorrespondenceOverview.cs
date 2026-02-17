namespace Altinn.Dd.Correspondence.Features.Get;

/// <summary>
/// An object representing an overview of a correspondence with enough details to drive the business process
/// </summary>
/// <param name="resourceId">The Resource Id associated with the correspondence service.</param>
/// <param name="sendersReference">A reference used by senders and receivers to identify a specific Correspondence using external identification methods.</param>
/// <param name="messageSender">An alternative name for the sender of the correspondence. The name, if set, will be displayed instead of the organization name in the inbox.</param>
/// <param name="content"></param>
/// <param name="requestedPublishTime">When the correspondence should become visible to the recipient.</param>
/// <param name="allowSystemDeleteAfter">When Altinn can remove the correspondence from its database.</param>
/// <param name="dueDateTime">When the recipient must reply to the correspondence.</param>
/// <param name="externalReferences">A list of references Senders can use to tell the recipient that the correspondence is related to the referenced item(s)
/// <br/>If an external reference of type DialogportenDialogId is set, a transmission will be created on the existing dialog instead.</param>
/// <param name="propertyList">User-defined properties related to the Correspondence</param>
/// <param name="replyOptions">Options for how the recipient can reply to the Correspondence</param>
/// <param name="notification"></param>
/// <param name="ignoreReservation">Specifies whether the correspondence can override reservation against digital communication in KRR</param>
/// <param name="published">Is null until the correspondence is published.</param>
/// <param name="isConfirmationNeeded">Specifies whether reading the correspondence needs to be confirmed by the recipient</param>
/// <param name="isConfidential">Specifies whether the correspondence is confidential</param>
/// <param name="recipient">The recipient of the correspondence.</param>
/// <param name="correspondenceId">Unique Id for this correspondence</param>
/// <param name="created">When the correspondence was created</param>
/// <param name="status"></param>
/// <param name="statusText">The current status text for the Correspondence</param>
/// <param name="statusChanged">Timestamp for when the Current Correspondence Status was changed</param>
/// <param name="notifications">An overview of the notifications for this correspondence</param>
/// <param name="altinn2CorrespondenceId">The identifier/reference from Altinn 2 for migrated correspondence. Will be null for correspondence created in Altinn 3.</param>
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
    int? Altinn2CorrespondenceId);

/// <summary>
/// Represents the content of a reportee element of the type correspondence.
/// </summary>
/// <param name="language">Gets or sets the language of the correspondence, specified according to ISO 639-1</param>
/// <param name="messageTitle">Gets or sets the correspondence message title. Subject.</param>
/// <param name="messageSummary">Gets or sets a summary text of the correspondence.</param>
/// <param name="messageBody">Gets or sets the main body of the correspondence.</param>
/// <param name="attachments">Gets or sets a list of attachments.</param>
public record CorrespondenceContent(
    string? Language,
    string? MessageTitle,
    string? MessageSummary,
    string? MessageBody,
    ICollection<CorrespondenceAttachment>? Attachments);

/// <summary>
/// Represents a binary attachment to a Correspondence
/// </summary>
/// <param name="fileName">The name of the attachment file.</param>
/// <param name="displayName">A logical name for the file, which will be shown in Altinn Inbox.</param>
/// <param name="isEncrypted">A value indicating whether the attachment is encrypted or not.</param>
/// <param name="checksum">MD5 checksum for file data.</param>
/// <param name="sendersReference">A reference value given to the attachment by the creator.</param>
/// <param name="id">A unique id for the correspondence attachment.</param>
/// <param name="dataLocationType"></param>
/// <param name="created">The date on which this attachment is created</param>
/// <param name="status"></param>
/// <param name="statusText">Current attachment status text description</param>
/// <param name="statusChanged">Timestamp for when the Current Attachment Status was changed</param>
/// <param name="expirationTime">When the attachment expires</param>
/// <param name="dataType">The attachment data type in MIME format</param>
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
    AltinnCorrespondenceAttachment = 0,
    ExternalStorage = 1,
}

/// <summary>
/// Represents the important statuses for an attachment
/// </summary>
public enum AttachmentStatus
{
    Initialized = 0,
    UploadProcessing = 1,
    Published = 2,
    Purged = 3,
    Failed = 4,
}

/// <summary>
/// Represents a reference to another item in the Altinn ecosystem
/// </summary>
/// <param name="referenceValue">The Reference Value</param>
/// <param name="referenceType"></param>
public record ExternalReference(
    string? ReferenceValue,
    ReferenceType ReferenceType);

/// <summary>
/// Defines what kind of reference
/// </summary>
public enum ReferenceType
{
    Generic = 0,
    AltinnAppInstance = 1,
    AltinnBrokerFileTransfer = 2,
    DialogportenDialogId = 3,
    DialogportenProcessId = 4,
    DialogportenTransmissionId = 5,
}

/// <summary>
/// Represents a ReplyOption with information provided by the sender.
/// <br/>A reply option is a way for recipients to respond to a correspondence in addition to the normal Read and Confirm operations
/// </summary>
/// <param name="linkURL">Gets or sets the URL to be used as a reply/response to a correspondence.</param>
/// <param name="linkText">Gets or sets the url text.</param>
public record CorrespondenceReplyOption(
    string? LinkURL,
    string? LinkText);

/// <summary>
/// Used to specify a single notification connected to a specific Correspondence during the Initialize Correspondence operation
/// </summary>
/// <param name="notificationTemplate"></param>
/// <param name="emailSubject">The emails subject for the main notification</param>
/// <param name="emailBody">The email body for the main notification</param>
/// <param name="emailContentType"></param>
/// <param name="smsBody">The sms body for the main notification</param>
/// <param name="sendReminder">Should a reminder be sent if the notification is not confirmed or opened</param>
/// <param name="reminderEmailSubject">The email subject to use for the reminder notification</param>
/// <param name="reminderEmailBody">The email body to use for the reminder notification</param>
/// <param name="reminderEmailContentType"></param>
/// <param name="reminderSmsBody">The sms body to use for the reminder notification</param>
/// <param name="notificationChannel"></param>
/// <param name="reminderNotificationChannel"></param>
/// <param name="sendersReference">Senders Reference for this notification</param>
/// <param name="requestedSendTime">The date and time for when the notification should be sent.</param>
/// <param name="customRecipients">A list of additional recipients for the notification. These are processed in addition to the Correspondence recipient;
/// <br/>if not set, only the Correspondence recipient receives the notification.</param>
/// <param name="customRecipient"></param>
/// <param name="overrideRegisteredContactInformation">When set to true, only CustomRecipients will be used for notifications, overriding the default correspondence recipient.
/// <br/>This flag can only be used when CustomRecipients is provided.
/// <br/>Default value is false (use default contact info + custom recipients).</param>
public record InitializeCorrespondenceNotification(
    NotificationTemplate NotificationTemplate,
    string? EmailSubject,
    string? EmailBody,
    EmailContentType EmailContentType,
    string? SmsBody,
    bool SendReminder,
    string? ReminderEmailSubject,
    string? ReminderEmailBody,
    EmailContentType ReminderEmailContentType,
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
    CustomMessage = 0,
    GenericAltinnMessage = 1,
}

public enum EmailContentType
{
    Plain = 0,
    Html = 1,
}

/// <summary>
/// Enum describing available notification channels.
/// </summary>
public enum NotificationChannel
{
    Email = 0,
    Sms = 1,
    EmailPreferred = 2,
    SmsPreferred = 3,
    EmailAndSms = 4,
}

/// <summary>
/// A class representing a a recipient of a notification
/// </summary>
/// <param name="emailAddress">the email address of the recipient</param>
/// <param name="mobileNumber">the mobileNumber of the recipient</param>
/// <param name="organizationNumber">the organization number of the recipient</param>
/// <param name="nationalIdentityNumber">The SSN of the recipient</param>
/// <param name="isReserved">Boolean indicating if the recipient is reserved</param>
public record NotificationRecipient(
    string? EmailAddress,
    string? MobileNumber,
    string? OrganizationNumber,
    string? NationalIdentityNumber,
    bool? IsReserved);


public record CorrespondenceNotificationOverview(
    Guid? NotificationOrderId,
    bool IsReminder);

/// <summary>
/// Represents the important statuses for an Correspondence
/// </summary>
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
}
