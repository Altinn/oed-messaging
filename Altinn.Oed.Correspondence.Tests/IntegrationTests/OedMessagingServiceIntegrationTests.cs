using System.Net.Http;
using Altinn.Oed.Correspondence.Authentication;
using Altinn.Oed.Correspondence.Extensions;
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Altinn.Oed.Correspondence.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Altinn.Oed.Correspondence.Tests.IntegrationTests;

/// <summary>
/// Integration tests for OedMessagingService using dependency injection
/// </summary>
public class OedMessagingServiceIntegrationTests
{
    /// <summary>
    /// Mock token provider for testing that returns a fake token.
    /// </summary>
    private class MockTokenProvider : IAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync()
        {
            return Task.FromResult("mock-token-for-testing");
        }
    }

    [Fact]
    public void AddOedCorrespondence_RegistersServicesCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();
        var tokenProvider = new MockTokenProvider();
        var hostBuilder = Host.CreateDefaultBuilder()
            .AddOedCorrespondence(settings, tokenProvider);

        // Act
        var host = hostBuilder.Build();

        // Assert
        var service = host.Services.GetService<IOedMessagingService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<Services.OedMessagingService>();

        var settingsService = host.Services.GetService<IOedNotificationSettings>();
        settingsService.Should().NotBeNull();
        settingsService.Should().BeSameAs(settings);

        var httpClient = host.Services.GetService<HttpClient>();
        httpClient.Should().NotBeNull();
    }

    [Fact]
    public void AddOedCorrespondence_WithTestSettings_ConfiguresCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithUseAltinnTestServers(true)
            .Build();
        var tokenProvider = new MockTokenProvider();

        // Act
        var host = Host.CreateDefaultBuilder()
            .AddOedCorrespondence(settings, tokenProvider)
            .Build();

        var service = host.Services.GetRequiredService<IOedMessagingService>();

        // Assert
        service.Should().NotBeNull();
        var messagingService = service as Services.OedMessagingService;
        messagingService!.ResourceId.Should().Be("test-resource");
        messagingService.Sender.Should().Be("test-sender");
    }

    [Fact]
    public void AddOedCorrespondence_WithProductionSettings_ConfiguresCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("prod-resource,prod-sender")
            .WithUseAltinnTestServers(false)
            .Build();
        var tokenProvider = new MockTokenProvider();

        // Act
        var host = Host.CreateDefaultBuilder()
            .AddOedCorrespondence(settings, tokenProvider)
            .Build();

        var service = host.Services.GetRequiredService<IOedMessagingService>();

        // Assert
        service.Should().NotBeNull();
        var messagingService = service as Services.OedMessagingService;
        messagingService!.ResourceId.Should().Be("prod-resource");
        messagingService.Sender.Should().Be("prod-sender");
    }

    [Fact]
    public async Task SendMessage_ThroughDependencyInjection_WorksCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();
        var tokenProvider = new MockTokenProvider();
        var host = Host.CreateDefaultBuilder()
            .AddOedCorrespondence(settings, tokenProvider)
            .Build();

        var service = host.Services.GetRequiredService<IOedMessagingService>();
        var messageDetails = OedMessageDetailsBuilder.Create().WithValidDefaults().Build();

        // Act & Assert
        // Note: This will fail with authentication error in real scenario,
        // but it validates the service is properly configured and can be called
        var exception = await Assert.ThrowsAsync<Exceptions.CorrespondenceServiceException>(
            () => service.SendMessage(messageDetails));

        exception.Message.Should().Contain("Could not send correspondence to Altinn 3");
    }

    [Fact]
    public void ServiceLifetime_IsSingleton()
    {
        // Arrange
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();
        var tokenProvider = new MockTokenProvider();
        var host = Host.CreateDefaultBuilder()
            .AddOedCorrespondence(settings, tokenProvider)
            .Build();

        // Act
        var service1 = host.Services.GetRequiredService<IOedMessagingService>();
        var service2 = host.Services.GetRequiredService<IOedMessagingService>();

        // Assert
        service1.Should().BeSameAs(service2);
    }

    [Fact]
    public void SettingsLifetime_IsSingleton()
    {
        // Arrange
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();
        var tokenProvider = new MockTokenProvider();
        var host = Host.CreateDefaultBuilder()
            .AddOedCorrespondence(settings, tokenProvider)
            .Build();

        // Act
        var settings1 = host.Services.GetRequiredService<IOedNotificationSettings>();
        var settings2 = host.Services.GetRequiredService<IOedNotificationSettings>();

        // Assert
        settings1.Should().BeSameAs(settings2);
        settings1.Should().BeSameAs(settings);
    }
}
