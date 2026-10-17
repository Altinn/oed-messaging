using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Tests.Models;

/// <summary>
/// Covers CorrespondenceResult&lt;T&gt; itself. It replaced three near-identical result types, and its Match
/// overload is public API that no other test exercises.
/// </summary>
public class CorrespondenceResultTests
{
    [Fact]
    public void Success_CarriesTheValueAndNoError()
    {
        var result = CorrespondenceResult<string>.Success("value");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("value", result.Value);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Failure_CarriesTheErrorAndNoValue()
    {
        var result = CorrespondenceResult<string>.Failure("went wrong");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal("went wrong", result.Error);
    }

    [Fact]
    public void Match_OnSuccess_RunsOnlyTheSuccessBranch()
    {
        var failureRan = false;

        var matched = CorrespondenceResult<int>.Success(41).Match(
            onSuccess: value => value + 1,
            onFailure: _ => { failureRan = true; return -1; });

        Assert.Equal(42, matched);
        Assert.False(failureRan);
    }

    [Fact]
    public void Match_OnFailure_RunsOnlyTheFailureBranch()
    {
        var successRan = false;

        var matched = CorrespondenceResult<int>.Failure("nope").Match(
            onSuccess: _ => { successRan = true; return "unreachable"; },
            onFailure: error => error.ToUpperInvariant());

        Assert.Equal("NOPE", matched);
        Assert.False(successRan);
    }

    [Fact]
    public void Match_OnFailure_DoesNotDereferenceTheMissingValue()
    {
        // Value is null on a failure, so Match must not pass it to the success branch.
        var result = CorrespondenceResult<string>.Failure("nope");

        var matched = result.Match(
            onSuccess: value => value.Length.ToString(),
            onFailure: _ => "handled");

        Assert.Equal("handled", matched);
    }

    [Fact]
    public void Success_And_PlainFailure_CarryNoErrorCodeOrStatus()
    {
        var success = CorrespondenceResult<string>.Success("value");
        var failure = CorrespondenceResult<string>.Failure("went wrong");

        Assert.Null(success.ErrorCode);
        Assert.Null(success.StatusCode);
        Assert.Null(failure.ErrorCode);
        Assert.Null(failure.StatusCode);
    }

    [Fact]
    public void Failure_WithCodes_CarriesThem()
    {
        var result = CorrespondenceResult<string>.Failure("not found", 1054, 404);

        Assert.True(result.IsFailure);
        Assert.Equal("not found", result.Error);
        Assert.Equal(1054, result.ErrorCode);
        Assert.Equal(404, result.StatusCode);
    }
}
