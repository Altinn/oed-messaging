using System.Net.Http;
using Altinn.Dd.Correspondence.Authentication;
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Altinn.Dd.Correspondence.Tests.IntegrationTests;

/// <summary>
/// Integration tests for DdMessagingService using dependency injection
/// </summary>
public class DdMessagingServiceIntegrationTests
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
    public void AddDdCorrespondence_RegistersServicesCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create().WithValidDefaults().Build();
        var tokenProvider = new MockTokenProvider();
        var hostBuilder = Host.CreateDefaultBuilder()
            .AddDdCorrespondence(settings, tokenProvider);

        // Act
        var host = hostBuilder.Build();

        // Assert
        var service = host.Services.GetService<IDdMessagingService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<Services.DdMessagingService>();

        var settingsService = host.Services.GetService<IDdNotificationSettings>();
        settingsService.Should().NotBeNull();
        settingsService.Should().BeSameAs(settings);

        var httpClient = host.Services.GetService<HttpClient>();
        httpClient.Should().NotBeNull();
    }

    [Fact]
    public void AddDdCorrespondence_WithTestSettings_ConfiguresCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithUseAltinnTestServers(true)
            .Build();
        var tokenProvider = new MockTokenProvider();

        // Act
        var host = Host.CreateDefaultBuilder()
            .AddDdCorrespondence(settings, tokenProvider)
            .Build();

        var service = host.Services.GetRequiredService<IDdMessagingService>();

        // Assert
        service.Should().NotBeNull();
        var messagingService = service as Services.DdMessagingService;
        messagingService!.ResourceId.Should().Be("test-resource");
        messagingService.Sender.Should().Be("test-sender");
    }

    [Fact]
    public void AddDdCorrespondence_WithProductionSettings_ConfiguresCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("prod-resource,prod-sender")
            .WithUseAltinnTestServers(false)
            .Build();
        var tokenProvider = new MockTokenProvider();

        // Act
        var host = Host.CreateDefaultBuilder()
            .AddDdCorrespondence(settings, tokenProvider)
            .Build();

        var service = host.Services.GetRequiredService<IDdMessagingService>();

        // Assert
        service.Should().NotBeNull();
        var messagingService = service as Services.DdMessagingService;
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
            .AddDdCorrespondence(settings, tokenProvider)
            .Build();

        var service = host.Services.GetRequiredService<IDdMessagingService>();
        var messageDetails = DdMessageDetailsBuilder.Create().WithValidDefaults().Build();

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
            .AddDdCorrespondence(settings, tokenProvider)
            .Build();

        // Act
        var service1 = host.Services.GetRequiredService<IDdMessagingService>();
        var service2 = host.Services.GetRequiredService<IDdMessagingService>();

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
            .AddDdCorrespondence(settings, tokenProvider)
            .Build();

        // Act
        var settings1 = host.Services.GetRequiredService<IDdNotificationSettings>();
        var settings2 = host.Services.GetRequiredService<IDdNotificationSettings>();

        // Assert
        settings1.Should().BeSameAs(settings2);
        settings1.Should().BeSameAs(settings);
    }
}
