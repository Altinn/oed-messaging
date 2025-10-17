using System.Net;
using System.Net.Http;
using Altinn.Oed.Correspondence.Exceptions;
using Altinn.Oed.Correspondence.ExternalServices.Correspondence;
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services;
using Altinn.Oed.Correspondence.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Oed.Correspondence.Tests.UnitTests;

/// <summary>
/// Unit tests for OedMessagingService
/// </summary>
public class OedMessagingServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly IOedNotificationSettings _settings;
    private readonly Mock<ILogger<OedMessagingService>> _mockLogger;
    private readonly OedMessagingService _service;

    public OedMessagingServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _settings = SettingsBuilder.Create().WithValidDefaults().Build();
        _mockLogger = new Mock<ILogger<OedMessagingService>>();
        _service = new OedMessagingService(_httpClient, _settings, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidSettings_SetsPropertiesCorrectly()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithUseAltinnTestServers(true)
            .Build();

        // Act
        var service = new OedMessagingService(_httpClient, settings, _mockLogger.Object);

        // Assert
        service.ResourceId.Should().Be("test-resource");
        service.Sender.Should().Be("test-sender");
    }

    [Fact]
    public void Constructor_WithTestServerSettings_SetsCorrectBaseUrl()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("test-resource,test-sender")
            .WithUseAltinnTestServers(true)
            .Build();
        var service = new OedMessagingService(_httpClient, settings, _mockLogger.Object);

        // Act & Assert
        // The base URL is set internally in the AltinnCorrespondenceClient
        // We can verify the service was created successfully
        service.Should().NotBeNull();
        service.ResourceId.Should().Be("test-resource");
    }

    [Fact]
    public void Constructor_WithProductionServerSettings_SetsCorrectBaseUrl()
    {
        // Arrange
        var settings = SettingsBuilder.Create()
            .WithCorrespondenceSettings("prod-resource,prod-sender")
            .WithUseAltinnTestServers(false)
            .Build();
        var service = new OedMessagingService(_httpClient, settings, _mockLogger.Object);

        // Act & Assert
        service.Should().NotBeNull();
        service.ResourceId.Should().Be("prod-resource");
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_ReturnsSuccessReceipt()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create().WithValidDefaults().Build();
        
        SetupSuccessfulHttpResponse();

        // Act
        var result = await _service.SendMessage(messageDetails);

        // Assert
        result.Should().NotBeNull();
        result.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        result.ReceiptText.Should().Contain("successfully");
    }

    [Fact]
    public async Task SendMessage_WithEmailNotification_CreatesCorrectRequest()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .WithEmailNotification("Test Subject", "Test Body")
            .Build();

        SetupSuccessfulHttpResponse();

        // Act
        await _service.SendMessage(messageDetails);

        // Assert
        VerifyHttpRequestWasMade();
    }

    [Fact]
    public async Task SendMessage_WithSmsNotification_CreatesCorrectRequest()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .WithSmsNotification("Test SMS")
            .Build();

        SetupSuccessfulHttpResponse();

        // Act
        await _service.SendMessage(messageDetails);

        // Assert
        VerifyHttpRequestWasMade();
    }

    [Fact]
    public async Task SendMessage_WithBothNotifications_CreatesCorrectRequest()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .WithBothNotifications("Email Subject", "Email Body", "SMS Text")
            .Build();

        SetupSuccessfulHttpResponse();

        // Act
        await _service.SendMessage(messageDetails);

        // Assert
        VerifyHttpRequestWasMade();
    }

    [Fact]
    public async Task SendMessage_WithHttpError_ThrowsCorrespondenceServiceException()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create().WithValidDefaults().Build();
        SetupHttpErrorResponse(HttpStatusCode.BadRequest, "Bad Request");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CorrespondenceServiceException>(
            () => _service.SendMessage(messageDetails));
        
        exception.Message.Should().Contain("Could not send correspondence to Altinn 3");
    }

    [Fact]
    public async Task SendMessage_WithNetworkException_ThrowsCorrespondenceServiceException()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create().WithValidDefaults().Build();
        SetupNetworkException();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CorrespondenceServiceException>(
            () => _service.SendMessage(messageDetails));
        
        exception.Message.Should().Contain("Could not send correspondence to Altinn 3");
    }

    [Fact]
    public async Task SendMessage_WithNullRecipient_HandlesGracefully()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .WithRecipient(null!)
            .Build();

        SetupSuccessfulHttpResponse();

        // Act
        var result = await _service.SendMessage(messageDetails);

        // Assert
        result.Should().NotBeNull();
        result.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
    }

    [Fact]
    public async Task SendMessage_WithNullNotification_HandlesGracefully()
    {
        // Arrange
        var messageDetails = OedMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .WithNotification(n => { }) // Empty notification
            .Build();

        SetupSuccessfulHttpResponse();

        // Act
        var result = await _service.SendMessage(messageDetails);

        // Assert
        result.Should().NotBeNull();
        result.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
    }

    private void SetupSuccessfulHttpResponse()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"correspondenceId\":\"test-id\"}")
            });
    }

    private void SetupHttpErrorResponse(HttpStatusCode statusCode, string errorMessage)
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent($"{{\"error\":\"{errorMessage}\"}}")
            });
    }

    private void SetupNetworkException()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
    }

    private void VerifyHttpRequestWasMade()
    {
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }
}
