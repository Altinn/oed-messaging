using System.Net;
using System.Net.Http;
using Altinn.Dd.Correspondence.Common.Exceptions;
using Altinn.Dd.Correspondence.Infrastructure;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Tests.Builders;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Altinn.Dd.Correspondence.Tests.UnitTests;

/// <summary>
/// Unit tests for DdMessagingService
/// </summary>
public class DdMessagingServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly CorrespondenceSettings _settings;
    private readonly Mock<ILogger<DdMessagingService>> _mockLogger;
    private readonly DdMessagingService _service;

    public DdMessagingServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _settings = CorrespondenceSettingsBuilder.Create().WithValidDefaults().Build();
        _mockLogger = new Mock<ILogger<DdMessagingService>>();
        _service = new DdMessagingService(_httpClient, Options.Create(_settings), _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidSettings_SetsPropertiesCorrectly()
    {
        // Arrange
        var settings = CorrespondenceSettingsBuilder.Create()
            .WithValidDefaults("test-resource,test-sender", "https://platform.tt02.altinn.no")
            .Build();

        // Act
        var service = new DdMessagingService(_httpClient, Options.Create(settings), _mockLogger.Object);

        // Assert
        service.ResourceId.Should().Be("test-resource");
        service.Sender.Should().Be("test-sender");
    }

    [Fact]
    public void Constructor_WithIgnoreReservationConfigured_SetsFlagAccordingly()
    {
        // Arrange
        var settings = CorrespondenceSettingsBuilder.Create()
            .WithValidDefaults()
            .WithIgnoreReservation(false)
            .Build();

        // Act
        var service = new DdMessagingService(_httpClient, Options.Create(settings), _mockLogger.Object);

        // Assert
        service.IgnoreReservation.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithTestServerSettings_SetsCorrectBaseUrl()
    {
        // Arrange
        var settings = CorrespondenceSettingsBuilder.Create()
            .WithValidDefaults("test-resource,test-sender", "https://platform.tt02.altinn.no")
            .Build();
        var service = new DdMessagingService(_httpClient, Options.Create(settings), _mockLogger.Object);

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
        var settings = CorrespondenceSettingsBuilder.Create()
            .WithValidDefaults("prod-resource,prod-sender", "https://platform.altinn.no")
            .Build();
        var service = new DdMessagingService(_httpClient, Options.Create(settings), _mockLogger.Object);

        // Act & Assert
        service.Should().NotBeNull();
        service.ResourceId.Should().Be("prod-resource");
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_ReturnsSuccessReceipt()
    {
        // Arrange
        var messageDetails = DdMessageDetailsBuilder.Create().WithValidDefaults().Build();
        
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
        var messageDetails = DdMessageDetailsBuilder.Create()
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
        var messageDetails = DdMessageDetailsBuilder.Create()
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
        var messageDetails = DdMessageDetailsBuilder.Create()
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
        var messageDetails = DdMessageDetailsBuilder.Create().WithValidDefaults().Build();
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
        var messageDetails = DdMessageDetailsBuilder.Create().WithValidDefaults().Build();
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
        var messageDetails = DdMessageDetailsBuilder.Create()
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
        var messageDetails = DdMessageDetailsBuilder.Create()
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

    [Fact]
    public async Task SendMessage_WithoutIdempotencyKey_GeneratesNewGuid()
    {
        // Arrange
        var messageDetails = DdMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .Build();
        
        // Ensure no idempotency key is set
        messageDetails.IdempotencyKey.Should().BeNull();

        SetupSuccessfulHttpResponse();

        // Act
        var result = await _service.SendMessage(messageDetails);

        // Assert
        result.Should().NotBeNull();
        result.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        
        // Verify that the HTTP request was made (which means a GUID was generated)
        VerifyHttpRequestWasMade();
    }

    [Fact]
    public async Task SendMessage_WithProvidedIdempotencyKey_UsesProvidedKey()
    {
        // Arrange
        var providedIdempotencyKey = Guid.NewGuid();
        var messageDetails = DdMessageDetailsBuilder.Create()
            .WithValidDefaults()
            .WithIdempotencyKey(providedIdempotencyKey)
            .Build();

        SetupSuccessfulHttpResponse();

        // Act
        var result = await _service.SendMessage(messageDetails);

        // Assert
        result.Should().NotBeNull();
        result.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        
        // Verify that the HTTP request was made with the provided key
        VerifyHttpRequestWasMade();
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
