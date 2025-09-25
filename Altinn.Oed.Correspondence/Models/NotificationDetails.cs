namespace Altinn.Oed.Correspondence.Models;

/// <summary>
/// This class holds details for creating notifications for a correspondence in Altinn 3.
/// Compatible with the existing Altinn 2 interface for seamless migration.
/// </summary>
public class NotificationDetails
{
    /// <summary>
    /// Gets or sets the text to use in an SMS notification.
    /// If provided, an SMS will be sent to the recipient.
    /// </summary>
    public string? SmsText { get; set; }

    /// <summary>
    /// Gets or sets the text to use in the subject of an email notification.
    /// If provided along with EmailBody, an email will be sent to the recipient.
    /// </summary>
    public string? EmailSubject { get; set; }

    /// <summary>
    /// Gets or sets the text to use in the body of an email notification.
    /// If provided along with EmailSubject, an email will be sent to the recipient.
    /// </summary>
    public string? EmailBody { get; set; }
}
