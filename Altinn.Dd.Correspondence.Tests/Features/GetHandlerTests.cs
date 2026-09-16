using Altinn.Dd.Correspondence.Features.Get;
using Altinn.Dd.Correspondence.HttpClients;
using System.Net;
using Altinn.Dd.Correspondence.Tests.TestSupport;


namespace Altinn.Dd.Correspondence.Tests.Features;

/// <summary>
/// Covers CorrespondenceGet.Handler: the round trip from a correspondence id to a mapped overview, and
/// the translation of an API problem document into a failure result.
/// </summary>
public class GetHandlerTests
{
    [Fact]
    public async Task Get_ReturnsTheMappedOverview()
    {
        var correspondenceId = Guid.NewGuid();
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Get, new CorrespondenceOverviewExt
        {
            ResourceId = "oed-correspondence",
            SendersReference = "caller-reference",
            CorrespondenceId = correspondenceId,
            Status = CorrespondenceStatusExt.Published,
            StatusText = "Published",
            Recipient = "0192:987654321"
        });
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(new Request(correspondenceId));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(correspondenceId, result.Value!.CorrespondenceId);
        Assert.Equal(CorrespondenceStatus.Published, result.Value.Status);
        Assert.Equal("0192:987654321", result.Value.Recipient);
    }

    [Fact]
    public async Task Get_WhenTheCorrespondenceIsUnknown_ReturnsFailureCarryingTheProblemDetail()
    {
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Get, HttpStatusCode.NotFound, "The correspondence was not found");
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(new Request(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("The correspondence was not found", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Get_RequestsTheCorrespondenceById()
    {
        var correspondenceId = Guid.NewGuid();
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Get, new CorrespondenceOverviewExt
        {
            ResourceId = "oed-correspondence",
            SendersReference = "caller-reference",
            CorrespondenceId = correspondenceId
        });
        var sut = new Handler(harness.Client());

        await sut.Handle(new Request(correspondenceId));

        Assert.Equal(1, harness.RequestCount);
        Assert.Equal($"/correspondence/api/v1/correspondence/{correspondenceId}", harness.LastRequestPath);
    }
}
