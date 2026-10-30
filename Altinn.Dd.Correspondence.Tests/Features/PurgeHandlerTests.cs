using Altinn.Dd.Correspondence.Features.Purge;
using Altinn.Dd.Correspondence.HttpClients;
using System.Net;
using Altinn.Dd.Correspondence.Tests.TestSupport;


namespace Altinn.Dd.Correspondence.Tests.Features;

/// <summary>
/// Covers CorrespondencePurge.Handler: the DELETE to the purge endpoint, the id Altinn returns, and the
/// translation of an API problem document into a failure result.
/// </summary>
public class PurgeHandlerTests
{
    [Fact]
    public async Task Purge_ReturnsThePurgedId()
    {
        var correspondenceId = Guid.NewGuid();
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Delete, correspondenceId);
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(new Request(correspondenceId));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(correspondenceId, result.Value);
    }

    [Fact]
    public async Task Purge_DeletesTheCorrespondenceById()
    {
        var correspondenceId = Guid.NewGuid();
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Delete, correspondenceId);
        var sut = new Handler(harness.Client());

        await sut.Handle(new Request(correspondenceId));

        Assert.Equal(1, harness.RequestCount);
        Assert.Equal($"/correspondence/api/v1/correspondence/{correspondenceId}/purge", harness.LastRequestPath);
    }

    [Fact]
    public async Task Purge_WhenTheCorrespondenceIsPublished_ReturnsFailureCarryingTheErrorCode()
    {
        // A sender can only purge before publishing; Altinn refuses afterwards with 1013.
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Delete, HttpStatusCode.BadRequest, "Sender cannot delete correspondence that has been published", 1013);
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(new Request(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("Sender cannot delete correspondence that has been published", result.Error);
        Assert.Equal(1013, result.ErrorCode);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Purge_WhenTheCorrespondenceIsUnknown_ReturnsFailureCarryingTheErrorCode()
    {
        // Also what a purge of an already purged correspondence can return: Altinn no longer finds it.
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Delete, HttpStatusCode.NotFound, "The requested correspondence was not found", 1001);
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(new Request(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(1001, result.ErrorCode);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Purge_WhenAltinnAnswersWithAnUndocumentedStatus_Throws()
    {
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Delete, HttpStatusCode.Forbidden, "Forbidden");
        var sut = new Handler(harness.Client());

        var exception = await Assert.ThrowsAsync<AltinnCorrespondenceException>(() => sut.Handle(new Request(Guid.NewGuid())));

        Assert.Equal((int)HttpStatusCode.Forbidden, exception.StatusCode);
    }

    [Fact]
    public async Task Purge_WhenTheTransportFails_ThrowsTheHttpRequestException()
    {
        var failure = new HttpRequestException("Connection reset");
        using var harness = new HandlerHarness().Throws(HttpMethod.Delete, failure);
        var sut = new Handler(harness.Client());

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => sut.Handle(new Request(Guid.NewGuid())));

        Assert.Same(failure, exception);
    }
}
