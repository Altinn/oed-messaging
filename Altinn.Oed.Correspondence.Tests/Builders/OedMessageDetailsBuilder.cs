using Altinn.Oed.Correspondence.Models;

namespace Altinn.Oed.Correspondence.Tests.Builders;

/// <summary>
/// Builder pattern for creating test instances of OedMessageDetails
/// </summary>
public class OedMessageDetailsBuilder
{
    private OedMessageDetails _messageDetails = new();

    public static OedMessageDetailsBuilder Create() => new();

    public OedMessageDetailsBuilder WithRecipient(string recipient)
    {
        _messageDetails.Recipient = recipient;
        return this;
    }

    public OedMessageDetailsBuilder WithTitle(string title)
    {
        _messageDetails.Title = title;
        return this;
    }

    public OedMessageDetailsBuilder WithSummary(string summary)
    {
        _messageDetails.Summary = summary;
        return this;
    }

    public OedMessageDetailsBuilder WithBody(string body)
    {
        _messageDetails.Body = body;
        return this;
    }

    public OedMessageDetailsBuilder WithSender(string sender)
    {
        _messageDetails.Sender = sender;
        return this;
    }

    public OedMessageDetailsBuilder WithVisibleDateTime(DateTime visibleDateTime)
    {
        _messageDetails.VisibleDateTime = visibleDateTime;
        return this;
    }

    public OedMessageDetailsBuilder WithShipmentDateTime(DateTime shipmentDateTime)
    {
        _messageDetails.ShipmentDatetime = shipmentDateTime;
        return this;
    }

    public OedMessageDetailsBuilder WithNotification(Action<NotificationDetailsBuilder> configure)
    {
        var notificationBuilder = new NotificationDetailsBuilder();
        configure(notificationBuilder);
        _messageDetails.Notification = notificationBuilder.Build();
        return this;
    }

    public OedMessageDetailsBuilder WithNoNotification()
    {
        _messageDetails.Notification = null!;
        return this;
    }

    public OedMessageDetailsBuilder WithAllowForwarding(bool allowForwarding)
    {
        _messageDetails.AllowForwarding = allowForwarding;
        return this;
    }

    public OedMessageDetailsBuilder WithIdempotencyKey(Guid idempotencyKey)
    {
        _messageDetails.IdempotencyKey = idempotencyKey;
        return this;
    }

    public OedMessageDetails Build() => _messageDetails;

    // Convenience methods for common test scenarios
    public OedMessageDetailsBuilder WithValidDefaults()
    {
        return WithRecipient("12345678901")
               .WithTitle("Test Correspondence")
               .WithSummary("Test Summary")
               .WithBody("Test Body Content")
               .WithSender("Test Organization")
               .WithVisibleDateTime(DateTime.Now.AddDays(1))
               .WithNotification(n => n.WithValidDefaults());
    }

    public OedMessageDetailsBuilder WithEmailNotification(string subject, string body)
    {
        return WithNotification(n => n.WithEmailSubject(subject).WithEmailBody(body));
    }

    public OedMessageDetailsBuilder WithSmsNotification(string smsText)
    {
        return WithNotification(n => n.WithSmsText(smsText));
    }

    public OedMessageDetailsBuilder WithBothNotifications(string emailSubject, string emailBody, string smsText)
    {
        return WithNotification(n => n
            .WithEmailSubject(emailSubject)
            .WithEmailBody(emailBody)
            .WithSmsText(smsText));
    }
}
