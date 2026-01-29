using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Altinn.Dd.Correspondence.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Altinn.Oed.Correspondence.Tests.Services;

public class DdCorrespondenceServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly IOptionsMonitor<DdCorrespondenceOptions> _optionsMonitor;
    private readonly DdCorrespondenceService _sut;

    // Test de ulike notifikasjonskanalene og formatering av mottaker
    public DdCorrespondenceServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        
        _optionsMonitor = Substitute.For<IOptionsMonitor<DdCorrespondenceOptions>>();
        _optionsMonitor.CurrentValue.Returns(new DdCorrespondenceOptions 
        {
            ResourceId = "test-resource-id",
            MaskinportenSettings = new MaskinportenSettings()
        });

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:3000");
        _sut = new DdCorrespondenceService(httpClient, _optionsMonitor);
    }

    [Fact]
    public async Task SendCorrespondence_WithValidDetails_ShouldSucceed()
    {
        var recipient = "test-recipient";
        var correspondence = ValidCorrespondenceDetails(recipient);
        CorrespondenceResponse(HttpStatusCode.OK, recipient);

        var result = await _sut.SendCorrespondence(correspondence);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Receipt);
        Assert.Equal(correspondence.IdempotencyKey, result.Receipt.IdempotencyKey);
        Assert.Equal(correspondence.SendersReference, result.Receipt.SendersReference);
    }

    [Fact]
    public async Task SendCorrespondence_400BadRequest_FailureResult()
    {
        var correspondence = ValidCorrespondenceDetails(string.Empty);
        CorrespondenceResponse(HttpStatusCode.BadRequest, string.Empty);

        var result = await _sut.SendCorrespondence(correspondence);

        Assert.True(result.IsFailure);
    }

    private static DdCorrespondenceDetails ValidCorrespondenceDetails(string recipient)
    {
        return new DdCorrespondenceDetails
        {
            Body = "This is a test correspondence body.",
            Notification = new NotificationDetails
            {
                EmailBody = "This is a test email body.",
                EmailSubject = "Test Email Subject",
                EmailContentType = Dd.Correspondence.Models.EmailContentType.Html,
                SmsText = "Test SMS Text"
            },
            Recipient = recipient,
            Sender = "test-sender",
            SendersReference = "test-reference",
            ShipmentDatetime = DateTime.UtcNow,
            Summary = "Test Summary",
            Title = "Test Title",
            AllowForwarding = true,
            IdempotencyKey = Guid.NewGuid(),
            IgnoreReservation = false,
            VisibleDateTime = DateTime.UtcNow.AddHours(1),
        };
    }

    private HttpResponseMessage CorrespondenceResponse(HttpStatusCode statusCode, string recipient)
    {
        var mockResponse = new HttpResponseMessage(statusCode);
        var responseData = new InitializeCorrespondencesResponseExt
        {
            Correspondences =
            [
                new InitializedCorrespondencesExt
                {
                    CorrespondenceId = Guid.NewGuid(),
                    Notifications =
                    [
                        new InitializedCorrespondencesNotificationsExt
                        {
                            Status = InitializedNotificationStatusExt.Success,
                            OrderId = Guid.NewGuid(),
                            IsReminder = false
                        }
                    ],
                    Recipient = recipient,
                    Status = CorrespondenceStatusExt.Initialized
                }
            ],
        };
        mockResponse.Content = new StringContent(JsonSerializer.Serialize(responseData), Encoding.UTF8, "application/json");
        _mockHttp.When("http://localhost:3000/*")
            .Respond(req => mockResponse);

        return mockResponse;
    }
}
