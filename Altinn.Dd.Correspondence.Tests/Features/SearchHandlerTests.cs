using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using System.Net;

namespace Altinn.Dd.Correspondence.Tests;

/// <summary>
/// Covers the guard clauses and error translation in Features.Search.Handler. Role and ResourceId
/// are mandatory on the Altinn 3 endpoint, so the handler must reject a query locally rather than
/// spend a call discovering it.
/// </summary>
public class SearchHandlerTests
{
    private static Query AValidQuery() => new(
        ResourceId: "oed-correspondence",
        Role: CorrespondencesRoleType.Sender);

    [Fact]
    public async Task Search_WithoutRole_FailsWithoutCallingTheApi()
    {
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Get, new CorrespondencesExt { Ids = [] });
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(AValidQuery() with { Role = null });

        Assert.True(result.IsFailure);
        Assert.Equal("Role is required for searching correspondences.", result.Error);
        Assert.Equal(0, harness.RequestCount);
    }

    [Fact]
    public async Task Search_WithoutResourceId_FailsWithoutCallingTheApi()
    {
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Get, new CorrespondencesExt { Ids = [] });
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(AValidQuery() with { ResourceId = null });

        Assert.True(result.IsFailure);
        Assert.Equal("ResourceId is required for searching correspondences.", result.Error);
        Assert.Equal(0, harness.RequestCount);
    }

    [Fact]
    public async Task Search_WithAValidQuery_ReturnsTheMatchingIds()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Get, new CorrespondencesExt { Ids = ids });
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(AValidQuery());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(ids, result.Value);
        Assert.Equal(1, harness.RequestCount);
    }

    [Fact]
    public async Task Search_ForwardsTheOptionalFiltersAsQueryParameters()
    {
        using var harness = new HandlerHarness().RespondsWith(HttpMethod.Get, new CorrespondencesExt { Ids = [] });
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(AValidQuery() with
        {
            SendersReference = "caller-reference",
            Status = CorrespondenceStatus.Published
        });

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("resourceId=oed-correspondence", harness.LastRequestQuery);
        Assert.Contains("sendersReference=caller-reference", harness.LastRequestQuery);
        // The enums go on the wire by their EnumMember name, not their numeric value.
        Assert.Contains("role=Sender", harness.LastRequestQuery);
        Assert.Contains("status=Published", harness.LastRequestQuery);
    }

    [Fact]
    public async Task Search_WhenApiRejectsTheRequest_ReturnsFailureCarryingTheProblemDetail()
    {
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Get, HttpStatusCode.BadRequest, "Invalid resource id");
        var sut = new Handler(harness.Client());

        var result = await sut.Handle(AValidQuery());

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid resource id", result.Error);
        Assert.Null(result.Value);
    }
}
