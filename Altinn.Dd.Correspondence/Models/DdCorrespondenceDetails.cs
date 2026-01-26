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
    /// Gets or sets the allow forwarding flag (default is false)
    /// </summary>
    public bool AllowForwarding { get; set; }

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
}
