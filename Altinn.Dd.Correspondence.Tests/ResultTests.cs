namespace Altinn.Dd.Correspondence.Tests;

/// <summary>
/// Covers Result&lt;T&gt; itself. It replaced three near-identical result types, and its Match
/// overload is public API that no other test exercises.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_CarriesTheValueAndNoError()
    {
        var result = Result<string>.Success("value");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("value", result.Value);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Failure_CarriesTheErrorAndNoValue()
    {
        var result = Result<string>.Failure("went wrong");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal("went wrong", result.Error);
    }

    [Fact]
    public void Match_OnSuccess_RunsOnlyTheSuccessBranch()
    {
        var failureRan = false;

        var matched = Result<int>.Success(41).Match(
            onSuccess: value => value + 1,
            onFailure: _ => { failureRan = true; return -1; });

        Assert.Equal(42, matched);
        Assert.False(failureRan);
    }

    [Fact]
    public void Match_OnFailure_RunsOnlyTheFailureBranch()
    {
        var successRan = false;

        var matched = Result<int>.Failure("nope").Match(
            onSuccess: _ => { successRan = true; return "unreachable"; },
            onFailure: error => error.ToUpperInvariant());

        Assert.Equal("NOPE", matched);
        Assert.False(successRan);
    }

    [Fact]
    public void Match_OnFailure_DoesNotDereferenceTheMissingValue()
    {
        // Value is null on a failure, so Match must not pass it to the success branch.
        var result = Result<string>.Failure("nope");

        var matched = result.Match(
            onSuccess: value => value.Length.ToString(),
            onFailure: _ => "handled");

        Assert.Equal("handled", matched);
    }
}
