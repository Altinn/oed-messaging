using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.Exceptions;
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Altinn.Dd.Correspondence.Tests.Extensions;

/// <summary>
/// Covers the resilience pipeline wired up by AddDdCorrespondenceService. These run through the
/// real DI stack, because the pipeline only exists once the client is registered.
///
/// The backoff is shortened to milliseconds so the suite does not spend seconds asleep; retry
/// counts and the decision of what to retry are left exactly as the library configures them,
/// since those are what the tests are checking.
/// </summary>
public class ResilienceTests
{
    private const int ExpectedRetries = 3;

    /// <summary>Counts attempts at the correspondence endpoint and replies from a scripted queue.</summary>
    private sealed class Endpoint
    {
        public int Attempts { get; private set; }

        public List<HttpResponseMessage> Discarded { get; } = [];

        public void Register(MockHttpMessageHandler mockHttp, Func<int, HttpStatusCode> statusForAttempt)
        {
            mockHttp.When(HttpMethod.Post, "*/correspondence/api/v1/correspondence")
                    .Respond(_ =>
                    {
                        Attempts++;
                        var status = statusForAttempt(Attempts);
                        var body = status == HttpStatusCode.OK
                            ? JsonSerializer.Serialize(new
                            {
                                correspondences = new[]
                                {
                                    new { correspondenceId = Guid.NewGuid(), status = 0, recipient = "0192:987654321" }
                                },
                                attachmentIds = Array.Empty<Guid>()
                            })
                            : JsonSerializer.Serialize(new { type = "about:blank", status = (int)status, detail = "upstream unavailable" });

                        var response = new HttpResponseMessage(status)
                        {
                            Content = new StringContent(body, Encoding.UTF8, "application/json")
                        };

                        // Every response the pipeline throws away should end up disposed.
                        if (status != HttpStatusCode.OK)
                        {
                            Discarded.Add(response);
                        }

                        return Task.FromResult(response);
                    });
        }
    }

    private static IHost BuildHost(
        MockHttpMessageHandler mockHttp,
        Action<HttpStandardResilienceOptions>? tighten = null) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDdCorrespondenceService(options =>
                {
                    options.ResourceId = "test-resource";
                    options.Environment = ApiEnvironment.Development;
                    options.MaskinportenSettings = new MaskinportenSettings
                    {
                        ClientId = "test-client",
                        Environment = "test",
                        EncodedJwk = MaskinportenStub.GenerateEncodedTestJwk()
                    };
                });

                // Registered after the library, so this wins. ConfigureAll avoids depending on the
                // pipeline's internal options name.
                services.ConfigureAll<HttpStandardResilienceOptions>(options =>
                {
                    options.Retry.Delay = TimeSpan.FromMilliseconds(1);
                    options.Retry.UseJitter = false;
                    tighten?.Invoke(options);
                });

                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                    options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = mockHttp));
            })
            .Build();

    private static DdCorrespondenceDetails Details() => new()
    {
        Recipient = "987654321",
        Title = "Title",
        Body = "Body",
        Notification = null
    };

    [Fact]
    public async Task TransientFailures_AreRetriedUntilTheCallSucceeds()
    {
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        var endpoint = new Endpoint();
        endpoint.Register(mockHttp, attempt => attempt < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);

        using var host = BuildHost(mockHttp);
        var result = await host.Services.GetRequiredService<IDdCorrespondenceService>()
            .SendCorrespondence(Details());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, endpoint.Attempts);
    }

    [Fact]
    public async Task RetriesAreBounded_AndTheFinalFailureSurfaces()
    {
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        var endpoint = new Endpoint();
        endpoint.Register(mockHttp, _ => HttpStatusCode.ServiceUnavailable);

        using var host = BuildHost(mockHttp);
        var service = host.Services.GetRequiredService<IDdCorrespondenceService>();

        // 503 is not one of the statuses the generated client turns into a failure result, so an
        // exhausted retry schedule surfaces as an exception rather than a result to inspect.
        var exception = await Assert.ThrowsAsync<AltinnCorrespondenceException>(
            () => service.SendCorrespondence(Details()));

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(1 + ExpectedRetries, endpoint.Attempts);
    }

    [Fact]
    public async Task DiscardedResponses_AreDisposed()
    {
        // Regression test. The hand-rolled policy this replaced left every retried response
        // undisposed, and the client reads with ResponseHeadersRead, so each one held its
        // connection until finalization.
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        var endpoint = new Endpoint();
        endpoint.Register(mockHttp, attempt => attempt < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);

        using var host = BuildHost(mockHttp);
        var result = await host.Services.GetRequiredService<IDdCorrespondenceService>()
            .SendCorrespondence(Details());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, endpoint.Discarded.Count);
        Assert.All(endpoint.Discarded, response =>
            Assert.Throws<ObjectDisposedException>(() => response.Content.Headers.ContentLength));
    }

    [Fact]
    public async Task ClientErrors_AreNotRetried()
    {
        // A 400 is the caller's fault and will fail identically every time; spending the retry
        // schedule on it only delays the error.
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        var endpoint = new Endpoint();
        endpoint.Register(mockHttp, _ => HttpStatusCode.BadRequest);

        using var host = BuildHost(mockHttp);
        var result = await host.Services.GetRequiredService<IDdCorrespondenceService>()
            .SendCorrespondence(Details());

        Assert.True(result.IsFailure);
        Assert.Equal("upstream unavailable", result.Error);
        Assert.Equal(1, endpoint.Attempts);
    }

    [Fact]
    public async Task TooManyRequests_IsRetried()
    {
        // 429 is transient by definition, so it must be retried rather than surfaced immediately.
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        var endpoint = new Endpoint();
        endpoint.Register(mockHttp, attempt => attempt < 2 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);

        using var host = BuildHost(mockHttp);
        var result = await host.Services.GetRequiredService<IDdCorrespondenceService>()
            .SendCorrespondence(Details());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, endpoint.Attempts);
    }

    /// <summary>Shrinks the timeouts so a slow upstream trips them without a slow test.</summary>
    private static void ShortTimeouts(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(200);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
    }

    [Fact]
    public async Task ATimeout_SurfacesAsACorrespondenceServiceException()
    {
        // The pipeline raises Polly's TimeoutRejectedException. Callers should not have to
        // reference Polly to catch it, so the library translates it at its own boundary.
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        mockHttp.When(HttpMethod.Post, "*/correspondence/api/v1/correspondence")
                .Respond(async _ =>
                {
                    await Task.Delay(800);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        using var host = BuildHost(mockHttp, ShortTimeouts);
        var service = host.Services.GetRequiredService<IDdCorrespondenceService>();

        var exception = await Assert.ThrowsAsync<CorrespondenceServiceException>(
            () => service.SendCorrespondence(Details()));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ATranslatedFailure_KeepsTheUnderlyingRejectionAsInnerException()
    {
        // Translating must not lose the detail needed to diagnose which limit was hit.
        using var mockHttp = new MockHttpMessageHandler().StubTokenExchange();
        mockHttp.When(HttpMethod.Post, "*/correspondence/api/v1/correspondence")
                .Respond(async _ =>
                {
                    await Task.Delay(800);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        using var host = BuildHost(mockHttp, ShortTimeouts);
        var service = host.Services.GetRequiredService<IDdCorrespondenceService>();

        var exception = await Assert.ThrowsAsync<CorrespondenceServiceException>(
            () => service.SendCorrespondence(Details()));

        Assert.IsAssignableFrom<ExecutionRejectedException>(exception.InnerException);
    }
}
