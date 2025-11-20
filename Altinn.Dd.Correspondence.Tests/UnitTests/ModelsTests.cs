using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Tests.Builders;
using FluentAssertions;
using Xunit;

namespace Altinn.Dd.Correspondence.Tests.UnitTests;

/// <summary>
/// Unit tests for model classes
/// </summary>
public class ModelsTests
{
    [Fact]
    public void DdMessageDetails_CanBeCreatedWithAllProperties()
    {
        // Arrange & Act
        var messageDetails = DdMessageDetailsBuilder.Create()
            .WithRecipient("12345678901")
            .WithTitle("Test Title")
            .WithSummary("Test Summary")
            .WithBody("Test Body")
            .WithSender("Test Sender")
            .WithVisibleDateTime(DateTime.Now.AddDays(1))
            .WithShipmentDateTime(DateTime.Now.AddDays(2))
            .WithAllowForwarding(true)
            .WithNotification(n => n.WithValidDefaults())
            .Build();

        // Assert
        messageDetails.Recipient.Should().Be("12345678901");
        messageDetails.Title.Should().Be("Test Title");
        messageDetails.Summary.Should().Be("Test Summary");
        messageDetails.Body.Should().Be("Test Body");
        messageDetails.Sender.Should().Be("Test Sender");
        messageDetails.VisibleDateTime.Should().BeCloseTo(DateTime.Now.AddDays(1), TimeSpan.FromSeconds(1));
        messageDetails.ShipmentDatetime.Should().BeCloseTo(DateTime.Now.AddDays(2), TimeSpan.FromSeconds(1));
        messageDetails.AllowForwarding.Should().BeTrue();
        messageDetails.Notification.Should().NotBeNull();
    }

    [Fact]
    public void DdMessageDetails_CanBeCreatedWithMinimalProperties()
    {
        // Arrange & Act
        var messageDetails = DdMessageDetailsBuilder.Create()
            .WithRecipient("12345678901")
            .WithTitle("Test Title")
            .WithBody("Test Body")
            .WithNoNotification()
            .Build();

        // Assert
        messageDetails.Recipient.Should().Be("12345678901");
        messageDetails.Title.Should().Be("Test Title");
        messageDetails.Body.Should().Be("Test Body");
        messageDetails.Summary.Should().BeNull();
        messageDetails.Sender.Should().BeNull();
        messageDetails.VisibleDateTime.Should().BeNull();
        messageDetails.ShipmentDatetime.Should().BeNull();
        messageDetails.AllowForwarding.Should().BeFalse();
        messageDetails.Notification.Should().BeNull();
    }

    [Fact]
    public void NotificationDetails_CanBeCreatedWithAllProperties()
    {
        // Arrange & Act
        var notification = NotificationDetailsBuilder.Create()
            .WithEmailSubject("Test Subject")
            .WithEmailBody("Test Body")
            .WithSmsText("Test SMS")
            .Build();

        // Assert
        notification.EmailSubject.Should().Be("Test Subject");
        notification.EmailBody.Should().Be("Test Body");
        notification.SmsText.Should().Be("Test SMS");
    }

    [Fact]
    public void NotificationDetails_CanBeCreatedWithEmailOnly()
    {
        // Arrange & Act
        var notification = NotificationDetailsBuilder.Create()
            .WithEmailOnly("Email Subject", "Email Body")
            .Build();

        // Assert
        notification.EmailSubject.Should().Be("Email Subject");
        notification.EmailBody.Should().Be("Email Body");
        notification.SmsText.Should().BeNull();
    }

    [Fact]
    public void NotificationDetails_CanBeCreatedWithSmsOnly()
    {
        // Arrange & Act
        var notification = NotificationDetailsBuilder.Create()
            .WithSmsOnly("SMS Text")
            .Build();

        // Assert
        notification.EmailSubject.Should().BeNull();
        notification.EmailBody.Should().BeNull();
        notification.SmsText.Should().Be("SMS Text");
    }

    [Fact]
    public void Settings_CanBeCreatedWithAllProperties()
    {
        // Arrange & Act
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithBaseUrl("https://platform.tt02.altinn.no")
            .Build();

        // Assert
        settings.CorrespondenceSettings.Should().Be("test-resource,test-sender");
        settings.BaseUrl.Should().Be("https://platform.tt02.altinn.no");
    }

    [Fact]
    public void Settings_CanBeCreatedWithProductionSettings()
    {
        // Arrange & Act
        var settings = SettingsBuilder.Create()
            .WithValidDefaults("prod-resource-id,prod-sender-org", "https://platform.altinn.no")
            .Build();

        // Assert
        settings.CorrespondenceSettings.Should().Be("prod-resource-id,prod-sender-org");
        settings.BaseUrl.Should().Be("https://platform.altinn.no");
    }

    [Fact]
    public void Settings_ImplementsIDdNotificationSettings()
    {
        // Arrange & Act
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();

        // Assert
        settings.Should().BeAssignableTo<Altinn.Dd.Correspondence.Models.Interfaces.IDdNotificationSettings>();
    }
}
