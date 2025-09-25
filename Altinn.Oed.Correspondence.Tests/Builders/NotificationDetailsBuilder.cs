using Altinn.Oed.Correspondence.Models;

namespace Altinn.Oed.Correspondence.Tests.Builders;

/// <summary>
/// Builder pattern for creating test instances of NotificationDetails
/// </summary>
public class NotificationDetailsBuilder
{
    private NotificationDetails _notificationDetails = new();

    public static NotificationDetailsBuilder Create() => new();

    public NotificationDetailsBuilder WithEmailSubject(string emailSubject)
    {
        _notificationDetails.EmailSubject = emailSubject;
        return this;
    }

    public NotificationDetailsBuilder WithEmailBody(string emailBody)
    {
        _notificationDetails.EmailBody = emailBody;
        return this;
    }

    public NotificationDetailsBuilder WithSmsText(string smsText)
    {
        _notificationDetails.SmsText = smsText;
        return this;
    }

    public NotificationDetailsBuilder WithValidDefaults()
    {
        return WithEmailSubject("Test Email Subject")
               .WithEmailBody("Test Email Body")
               .WithSmsText("Test SMS Text");
    }

    public NotificationDetailsBuilder WithEmailOnly(string subject, string body)
    {
        return WithEmailSubject(subject).WithEmailBody(body);
    }

    public NotificationDetailsBuilder WithSmsOnly(string smsText)
    {
        return WithSmsText(smsText);
    }

    public NotificationDetails Build() => _notificationDetails;
}
