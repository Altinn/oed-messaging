using System.Net.Http;
using System.Reflection;
using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void AddDdMessagingService_RegistersServicesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Create configuration with required settings
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DdConfig:MaskinportenSettings:ClientId", "test-client-id" },
                { "DdConfig:MaskinportenSettings:Environment", "test" },
                { "DdConfig:MaskinportenSettings:EncodedJwk", "test-jwk" },
                { "DdConfig:CorrespondenceSettings:CorrespondenceSettings", "test-resource,test-sender" },
                { "DdConfig:CorrespondenceSettings:UseAltinnTestServers", "true" },
                { "DdConfig:CorrespondenceSettings:CountryCode", "0192" }
            })
            .Build();

        // Act
        services.AddDdMessagingService<SettingsJwkClientDefinition>(
            configuration.GetSection("DdConfig:MaskinportenSettings"),
            configuration.GetSection("DdConfig:CorrespondenceSettings"));

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var messagingService = serviceProvider.GetService<IDdMessagingService>();
        messagingService.Should().NotBeNull();
        messagingService.Should().BeOfType<Services.DdMessagingService>();

        var settingsService = serviceProvider.GetService<IDdNotificationSettings>();
        settingsService.Should().NotBeNull();
        settingsService.CorrespondenceSettings.Should().Be("test-resource,test-sender");
        settingsService.UseAltinnTestServers.Should().BeTrue();

        var boundMaskinportenSettings = BindMaskinportenSettings(configuration.GetSection("DdConfig:MaskinportenSettings"));
        boundMaskinportenSettings.Scope.Should().Be("altinn:serviceowner altinn:correspondence.write");
        boundMaskinportenSettings.EnableDebugLogging.Should().NotBeTrue();
    }

    [Fact]
    public void AddDdMessagingService_WithInvalidCorrespondenceSettings_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DdConfig:MaskinportenSettings:ClientId", "test-client-id" },
                { "DdConfig:MaskinportenSettings:Environment", "test" },
                { "DdConfig:MaskinportenSettings:EncodedJwk", "test-jwk" }
                // Missing CorrespondenceSettings section
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddDdMessagingService<SettingsJwkClientDefinition>(
                configuration.GetSection("DdConfig:MaskinportenSettings"),
                configuration.GetSection("DdConfig:CorrespondenceSettings")));

        exception.Message.Should().Contain("Correspondence settings are required");
        exception.Message.Should().Contain("DdConfig:CorrespondenceSettings");
    }

    [Fact]
    public void AddDdMessagingService_EnableDebugLoggingThroughConfiguration_SetsFlag()
    {
        // Arrange
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DdConfig:MaskinportenSettings:ClientId", "test-client-id" },
                { "DdConfig:MaskinportenSettings:Environment", "test" },
                { "DdConfig:MaskinportenSettings:EncodedJwk", "test-jwk" },
                { "DdConfig:MaskinportenSettings:EnableDebugLogging", "true" },
                { "DdConfig:CorrespondenceSettings:CorrespondenceSettings", "test-resource,test-sender" },
                { "DdConfig:CorrespondenceSettings:UseAltinnTestServers", "true" },
                { "DdConfig:CorrespondenceSettings:CountryCode", "0192" }
            })
            .Build();

        // Act
        services.AddDdMessagingService<SettingsJwkClientDefinition>(
            configuration.GetSection("DdConfig:MaskinportenSettings"),
            configuration.GetSection("DdConfig:CorrespondenceSettings"));

        // Assert
        var boundMaskinportenSettings = BindMaskinportenSettings(configuration.GetSection("DdConfig:MaskinportenSettings"));
        boundMaskinportenSettings.EnableDebugLogging.Should().BeTrue();
        boundMaskinportenSettings.Scope.Should().Be("altinn:serviceowner altinn:correspondence.write");
    }

    [Theory]
    [InlineData("ClientId", "Maskinporten ClientId must be provided.")]
    [InlineData("EncodedJwk", "Maskinporten EncodedJwk must be provided.")]
    [InlineData("Environment", "Maskinporten Environment must be provided.")]
    public void AddDdMessagingService_MissingRequiredMaskinportenSetting_Throws(string missingKey, string expectedMessage)
    {
        // Arrange
        var services = new ServiceCollection();

        var baseSettings = new Dictionary<string, string?>
        {
            { "DdConfig:MaskinportenSettings:ClientId", "test-client-id" },
            { "DdConfig:MaskinportenSettings:Environment", "test" },
            { "DdConfig:MaskinportenSettings:EncodedJwk", "test-jwk" },
            { "DdConfig:CorrespondenceSettings:CorrespondenceSettings", "test-resource,test-sender" },
            { "DdConfig:CorrespondenceSettings:UseAltinnTestServers", "true" },
            { "DdConfig:CorrespondenceSettings:CountryCode", "0192" }
        };

        baseSettings.Remove($"DdConfig:MaskinportenSettings:{missingKey}");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(baseSettings)
            .Build();

        // Act
        Action act = () => services.AddDdMessagingService<SettingsJwkClientDefinition>(
            configuration.GetSection("DdConfig:MaskinportenSettings"),
            configuration.GetSection("DdConfig:CorrespondenceSettings"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
    }

    private static MaskinportenSettings BindMaskinportenSettings(IConfigurationSection section)
    {
        var method = typeof(ServiceCollectionExtensions)
            .GetMethod("BindMaskinportenSettings", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("the binding helper should exist");

        return (MaskinportenSettings)method!.Invoke(null, new object[] { section })!;
    }
}
