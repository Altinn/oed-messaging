using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using RichardSzalay.MockHttp;
using System.Text;
using System.Text.Json;

namespace Altinn.Dd.Correspondence.Tests.Services;

public class DdCorrespondenceServiceIntegrationTests
{
    [Fact]
    public async Task Service_Should_Be_Resolvable_And_Send_Request_Correctly()
    {
        using var mockHttp = new MockHttpMessageHandler();

        var receipt = new InitializeCorrespondencesResponseExt
        {
            Correspondences = [new InitializedCorrespondencesExt { CorrespondenceId = Guid.NewGuid(), Status = CorrespondenceStatusExt.Initialized }]
        };

        mockHttp.When(HttpMethod.Post, "https://platform.tt02.altinn.no/correspondence/api/v1/correspondence")
                .Respond("application/json", JsonSerializer.Serialize(receipt));

        mockHttp.StubTokenExchange();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, builder) =>
            {
                var options = new DdCorrespondenceOptions
                {
                    ResourceId = "test-resource-urn",
                    MaskinportenSettings = new MaskinportenSettings
                    {
                        ClientId = "test-client-id",
                        EncodedJwk = MaskinportenStub.GenerateEncodedTestJwk(),
                        Environment = "test"
                    },
                    Environment = ApiEnvironment.Development
                };

                var json = JsonSerializer.Serialize(new { DdCorrespondence = options });
                builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddDdCorrespondenceService("DdCorrespondence");

                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(builder =>
                    {
                        builder.PrimaryHandler = mockHttp;
                    });
                });
            })
            .Build();

        var service = host.Services.GetRequiredService<IDdCorrespondenceService>();

        var details = new DdCorrespondenceDetails
        {
            Recipient = "01010112345",
            Title = "Integration Test",
            Body = "Testing DI wiring",
            IdempotencyKey = Guid.NewGuid(),
            AllowForwarding = false,
            IgnoreReservation = false,
        };

        var result = await service.SendCorrespondence(details);

        Assert.True(result.IsSuccess, $"Failed with error: {result.Error}");
        Assert.Equal(details.IdempotencyKey, result.Value!.IdempotencyKey);

        mockHttp.VerifyNoOutstandingExpectation();
    }
}
