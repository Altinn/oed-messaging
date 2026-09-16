using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Altinn.Dd.Correspondence.Tests;

/// <summary>
/// Builds a feature handler over a mocked transport, so the handler's own logic - recipient
/// formatting, notification assembly, query validation, error translation - is exercised for real
/// and only the network is faked.
/// </summary>
internal sealed class HandlerHarness : IDisposable
{
    public const string ResourceId = "test-resource";

    private readonly MockHttpMessageHandler _mockHttp = new();

    /// <summary>The raw JSON body of the last request the handler sent, or null if it sent none.</summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>The query string of the last request the handler sent, or null if it sent none.</summary>
    public string? LastRequestQuery { get; private set; }

    /// <summary>The absolute path of the last request the handler sent, or null if it sent none.</summary>
    public string? LastRequestPath { get; private set; }

    /// <summary>How many requests the handler actually sent.</summary>
    public int RequestCount { get; private set; }

    /// <summary>Responds 200 with <paramref name="responseBody"/> serialized as JSON.</summary>
    public HandlerHarness RespondsWith<T>(HttpMethod method, T responseBody)
        => Responds(method, HttpStatusCode.OK, JsonSerializer.Serialize(responseBody));

    /// <summary>Responds with an RFC 7807 problem document, the shape the client turns into a failure result.</summary>
    public HandlerHarness RespondsWithProblem(HttpMethod method, HttpStatusCode status, string detail)
        => Responds(method, status, JsonSerializer.Serialize(new { type = "about:blank", title = "Bad Request", status = (int)status, detail }));

    private HandlerHarness Responds(HttpMethod method, HttpStatusCode status, string json)
    {
        _mockHttp.When(method, "*/correspondence/api/v1/correspondence*")
                 .Respond(async request =>
                 {
                     RequestCount++;
                     LastRequestQuery = request.RequestUri?.Query;
                     LastRequestPath = request.RequestUri?.AbsolutePath;
                     LastRequestBody = request.Content is null
                         ? null
                         : await request.Content.ReadAsStringAsync();

                     return new HttpResponseMessage(status)
                     {
                         Content = new StringContent(json, Encoding.UTF8, "application/json")
                     };
                 });
        return this;
    }

    /// <summary>The request body the handler sent, deserialized back into the wire contract.</summary>
    public InitializeCorrespondencesExt SentCorrespondence()
    {
        Assert.NotNull(LastRequestBody);
        var sent = JsonSerializer.Deserialize<InitializeCorrespondencesExt>(LastRequestBody!);
        Assert.NotNull(sent);
        return sent!;
    }

    public AltinnCorrespondenceClient Client() =>
        new(new HttpClient(_mockHttp) { BaseAddress = new Uri("https://platform.tt02.altinn.no") });

    public IOptionsMonitor<DdCorrespondenceOptions> Options()
    {
        var monitor = Substitute.For<IOptionsMonitor<DdCorrespondenceOptions>>();
        monitor.CurrentValue.Returns(new DdCorrespondenceOptions
        {
            ResourceId = ResourceId,
            MaskinportenSettings = new MaskinportenSettings { ClientId = "test-client", Environment = "test" },
            Environment = ApiEnvironment.Development
        });
        return monitor;
    }

    public void Dispose() => _mockHttp.Dispose();
}
