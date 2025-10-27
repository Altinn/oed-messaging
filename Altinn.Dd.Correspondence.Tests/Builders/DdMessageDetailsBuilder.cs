using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Tests.Builders;

/// <summary>
/// Builder pattern for creating test instances of DdMessageDetails
/// </summary>
public class DdMessageDetailsBuilder
{
    private DdMessageDetails _messageDetails = new();

    public static DdMessageDetailsBuilder Create() => new();

    public DdMessageDetailsBuilder WithRecipient(string recipient)
    {
        _messageDetails.Recipient = recipient;
        return this;
    }

    public DdMessageDetailsBuilder WithTitle(string title)
    {
        _messageDetails.Title = title;
        return this;
    }

    public DdMessageDetailsBuilder WithSummary(string summary)
    {
        _messageDetails.Summary = summary;
        return this;
    }

    public DdMessageDetailsBuilder WithBody(string body)
    {
        _messageDetails.Body = body;
        return this;
    }

    public DdMessageDetailsBuilder WithSender(string sender)
    {
        _messageDetails.Sender = sender;
        return this;
    }

    public DdMessageDetailsBuilder WithVisibleDateTime(DateTime visibleDateTime)
    {
        _messageDetails.VisibleDateTime = visibleDateTime;
        return this;
    }

    public DdMessageDetailsBuilder WithShipmentDateTime(DateTime shipmentDateTime)
    {
        _messageDetails.ShipmentDatetime = shipmentDateTime;
        return this;
    }

    public DdMessageDetailsBuilder WithNotification(Action<NotificationDetailsBuilder> configure)
    {
        var notificationBuilder = new NotificationDetailsBuilder();
        configure(notificationBuilder);
        _messageDetails.Notification = notificationBuilder.Build();
        return this;
    }

    public DdMessageDetailsBuilder WithNoNotification()
    {
        _messageDetails.Notification = null!;
        return this;
    }

    public DdMessageDetailsBuilder WithAllowForwarding(bool allowForwarding)
    {
        _messageDetails.AllowForwarding = allowForwarding;
        return this;
    }

    public DdMessageDetailsBuilder WithIdempotencyKey(Guid idempotencyKey)
    {
        _messageDetails.IdempotencyKey = idempotencyKey;
        return this;
    }

    public DdMessageDetails Build() => _messageDetails;

    // Convenience methods for common test scenarios
    public DdMessageDetailsBuilder WithValidDefaults()
    {
        return WithRecipient("12345678901")
               .WithTitle("Test Correspondence")
               .WithSummary("Test Summary")
               .WithBody("Test Body Content")
               .WithSender("Test Organization")
               .WithVisibleDateTime(DateTime.Now.AddDays(1))
               .WithNotification(n => n.WithValidDefaults());
    }

    public DdMessageDetailsBuilder WithEmailNotification(string subject, string body)
    {
        return WithNotification(n => n.WithEmailSubject(subject).WithEmailBody(body));
    }

    public DdMessageDetailsBuilder WithSmsNotification(string smsText)
    {
        return WithNotification(n => n.WithSmsText(smsText));
    }

    public DdMessageDetailsBuilder WithBothNotifications(string emailSubject, string emailBody, string smsText)
    {
        return WithNotification(n => n
            .WithEmailSubject(emailSubject)
            .WithEmailBody(emailBody)
            .WithSmsText(smsText));
    }
}
