namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// The outcome of requesting a notification for a correspondence.
/// </summary>
public enum InitializedNotificationStatus
{
    /// <summary>The notification order was created.</summary>
    Success = 0,

    /// <summary>No contact information was registered for the recipient, so nothing was sent.</summary>
    MissingContact = 1,

    /// <summary>The notification order could not be created.</summary>
    Failure = 2,
};

/// <summary>
/// The lifecycle status of a correspondence.
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
};
