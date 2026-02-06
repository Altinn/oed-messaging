using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Altinn.Dd.Correspondence.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using RichardSzalay.MockHttp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Altinn.Oed.Correspondence.Tests.Services;

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

        mockHttp.When(HttpMethod.Get, "*/.well-known/oauth-authorization-server")
                .Respond("application/json", JsonSerializer.Serialize(new
                {
                    issuer = "https://test.maskinporten.no/",
                    token_endpoint = "https://test.maskinporten.no/token"
                }));
        // authentication/api/v1/exchange/maskinporten

        var tokenResponse = new
        {
            access_token = "dummy-integration-test-token",
            token_type = "Bearer",
            expires_in = 3599,
            scope = "altinn:serviceowner/correspondence.write"
        };

        mockHttp.When(HttpMethod.Post, "*/token")
            .Respond("application/json", JsonSerializer.Serialize(tokenResponse));

        var altinnTokenResponse = GenerateEncodedTestJwk();

        mockHttp.When(HttpMethod.Get, "https://platform.tt02.altinn.no/authentication/api/v1/exchange/maskinporten")
                .Respond("application/json", JsonSerializer.Serialize(altinnTokenResponse));

        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, builder) =>
            {
                var options = new DdCorrespondenceOptions
                {
                    ResourceId = "test-resource-urn",
                    MaskinportenSettings = new MaskinportenSettings
                    {
                        ClientId = "test-client-id",
                        EncodedJwk = GenerateEncodedTestJwk(),
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
        Assert.Equal(details.IdempotencyKey, result.Receipt!.IdempotencyKey);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    private static string GenerateEncodedTestJwk()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(true);

        var jwk = new
        {
            kty = "RSA",
            use = "sig",
            kid = "test-key",
            alg = "RS256",
            n = Base64Url(p.Modulus!),
            e = Base64Url(p.Exponent!),
            d = Base64Url(p.D!),
            p = Base64Url(p.P!),
            q = Base64Url(p.Q!),
            dp = Base64Url(p.DP!),
            dq = Base64Url(p.DQ!),
            qi = Base64Url(p.InverseQ!)
        };

        var json = JsonSerializer.Serialize(jwk);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}