namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// This class holds the details needed to create a new correspondence in Altinn 3.
/// Compatible with the existing Altinn 2 interface for seamless migration.
/// </summary>
public class DdCorrespondenceDetails
{
    /// <summary>
    /// Gets or sets the correspondence recipient. This should be an organization number or social security number.
    /// </summary>
    public string? Recipient { get; set; }

    /// <summary>
    /// Gets or sets the title of the correspondence.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the correspondence summary. 
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the main body of the correspondence.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the sender of the correspondence.
    /// </summary>
    public string? Sender { get; set; }

    /// <summary>
    /// Gets or sets the visible date time for the message
    /// </summary>
    public DateTime? VisibleDateTime { get; set; }

    /// <summary>
    /// Gets or sets the visible date time for notifications (sms, email)
    /// </summary>
    public DateTime? ShipmentDatetime { get; set; }

    /// <summary>
    /// Gets or sets the details needed to create notifications.
    /// </summary>
    public NotificationDetails? Notification { get; set; } = new();

    /// <summary>
    /// Gets or sets the idempotency key to prevent duplicate correspondence creation.
    /// If not provided, a new GUID will be generated automatically.
    /// </summary>
    public Guid IdempotencyKey { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Specifies whether the correspondence can override reservation against digital communication in KRR.
    /// </summary>
    public bool IgnoreReservation { get; set; }

    /// <summary>
    /// Gets or sets the sender's reference for tracking and identification purposes.
    /// If not provided, a reference will be automatically generated.
    /// </summary>
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the id of an existing Dialogporten dialog. When set, the correspondence is added
    /// to that dialog as a transmission instead of creating a new dialog. The dialog must belong to
    /// the same recipient and to a resource with the same service owner.
    /// </summary>
    /// <remarks>
    /// Altinn creates a correspondence's dialog after the correspondence is published, so the id is
    /// not in the send receipt. Read it later with <see cref="Services.IDdCorrespondenceService.GetDialogId"/>.
    /// </remarks>
    public Guid? DialogId { get; set; }

    /// <summary>
    /// Gets or sets the kind of transmission the correspondence becomes on the dialog given by
    /// <see cref="DialogId"/>. Only valid together with <see cref="DialogId"/>. If not provided,
    /// Altinn uses <see cref="Models.TransmissionType.Information"/>.
    /// </summary>
    public TransmissionType? TransmissionType { get; set; }
}
