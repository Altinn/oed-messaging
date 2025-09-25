namespace Altinn.Oed.Correspondence.Models;

/// <summary>
/// This class holds the details needed to create a new correspondence in Altinn.
/// </summary>
public class OedMessageDetails
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
    public NotificationDetails Notification { get; set; } = new();

    /// <summary>
    /// Gets or sets the allow forwarding flag (default is false)
    /// </summary>
    public bool AllowForwarding { get; set; }
}
