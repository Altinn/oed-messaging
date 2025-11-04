using System.Net.Http;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Altinn.Dd.Correspondence.Tests.IntegrationTests;

/// <summary>
/// Integration tests for DdMessagingService
/// Note: These tests validate the service directly without the full DI registration
/// since Maskinporten HttpClient registration requires actual authentication setup.
/// For full end-to-end tests with AddDdMessagingService, see the SendDdCorrespondence example project.
/// </summary>
public class DdMessagingServiceIntegrationTests
{
    [Fact]
    public void DdMessagingService_WithValidSettings_InitializesCorrectly()
    {
        // Arrange
        var httpClient = new HttpClient();
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithUseAltinnTestServers(true)
            .Build();
        var mockLogger = new Mock<ILogger<DdMessagingService>>();

        // Act
        var service = new DdMessagingService(httpClient, settings, mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.ResourceId.Should().Be("test-resource");
        service.Sender.Should().Be("test-sender");
    }

    [Fact]
    public void DdMessagingService_WithTestSettings_SetsCorrectEnvironment()
    {
        // Arrange
        var httpClient = new HttpClient();
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithUseAltinnTestServers(true)
            .Build();
        var mockLogger = new Mock<ILogger<DdMessagingService>>();

        // Act
        var service = new DdMessagingService(httpClient, settings, mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.ResourceId.Should().Be("test-resource");
        service.Sender.Should().Be("test-sender");
    }

    [Fact]
    public void DdMessagingService_WithProductionSettings_SetsCorrectEnvironment()
    {
        // Arrange
        var httpClient = new HttpClient();
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("prod-resource,prod-sender")
            .WithUseAltinnTestServers(false)
            .Build();
        var mockLogger = new Mock<ILogger<DdMessagingService>>();

        // Act
        var service = new DdMessagingService(httpClient, settings, mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.ResourceId.Should().Be("prod-resource");
        service.Sender.Should().Be("prod-sender");
    }

    [Fact]
    public void DdMessagingService_WithInvalidSettings_ThrowsException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("") // Invalid: empty settings
            .Build();
        var mockLogger = new Mock<ILogger<DdMessagingService>>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new DdMessagingService(httpClient, settings, mockLogger.Object));
        
        exception.Message.Should().Contain("validation failed");
    }

    [Fact]
    public void DdMessagingService_WithNullHttpClient_ThrowsException()
    {
        // Arrange
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();
        var mockLogger = new Mock<ILogger<DdMessagingService>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new DdMessagingService(null!, settings, mockLogger.Object));
    }

    [Fact]
    public void DdMessagingService_WithNullSettings_ThrowsException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockLogger = new Mock<ILogger<DdMessagingService>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new DdMessagingService(httpClient, null!, mockLogger.Object));
    }

    [Fact]
    public void DdMessagingService_WithNullLogger_ThrowsException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new DdMessagingService(httpClient, settings, null!));
    }
}
