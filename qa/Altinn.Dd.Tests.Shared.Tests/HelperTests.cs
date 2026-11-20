using System.Text.Json;
using Xunit;

namespace Altinn.Dd.Tests.Shared.Tests;

public class JsonTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void GetStringOrEmpty_ReadsAString()
        => Assert.Equal("hi", Json.GetStringOrEmpty(Parse("""{"a":"hi"}"""), "a"));

    [Fact]
    public void GetStringOrEmpty_TreatsAbsentOrWrongTypeAsEmpty()
    {
        // Both gates parse JSON they do not own, so a shape surprise must read as absent.
        Assert.Equal("", Json.GetStringOrEmpty(Parse("""{"a":"hi"}"""), "missing"));
        Assert.Equal("", Json.GetStringOrEmpty(Parse("""{"a":42}"""), "a"));
        Assert.Equal("", Json.GetStringOrEmpty(Parse("""{"a":null}"""), "a"));
    }

    [Fact]
    public void GetIntOrNull_ReadsANumberAndNothingElse()
    {
        Assert.Equal(7, Json.GetIntOrNull(Parse("""{"n":7}"""), "n"));
        Assert.Null(Json.GetIntOrNull(Parse("""{"n":"7"}"""), "n"));
        Assert.Null(Json.GetIntOrNull(Parse("""{"n":7}"""), "missing"));
    }

    [Fact]
    public void GetIntOrNull_TreatsANumberThatDoesNotFitAnIntAsAbsent()
    {
        Assert.Null(Json.GetIntOrNull(Parse("""{"n":2147483648}"""), "n"));
        Assert.Null(Json.GetIntOrNull(Parse("""{"n":7.5}"""), "n"));
    }

    [Fact]
    public void GetBool_IsTrueOnlyForJsonTrue()
    {
        Assert.True(Json.GetBool(Parse("""{"b":true}"""), "b"));
        Assert.False(Json.GetBool(Parse("""{"b":false}"""), "b"));
        Assert.False(Json.GetBool(Parse("""{"b":"true"}"""), "b"));
        Assert.False(Json.GetBool(Parse("""{"b":true}"""), "missing"));
    }
}

public class MarkdownTests
{
    [Fact]
    public void Cell_EscapesPipes()
        => Assert.Equal("a \\| b", Markdown.Cell("a | b"));

    [Fact]
    public void Cell_CollapsesNewlines()
    {
        // A multi-line SonarQube message would otherwise end the table row early and silently
        // corrupt every row after it.
        Assert.Equal("one two", Markdown.Cell("one\ntwo"));
        Assert.Equal("one two", Markdown.Cell("one\rtwo"));
        Assert.Equal("one  two", Markdown.Cell("one\r\ntwo"));
    }

    [Fact]
    public void Cell_LeavesOrdinaryTextAlone()
        => Assert.Equal("OpenTelemetry.Api", Markdown.Cell("OpenTelemetry.Api"));
}

public class RepoRootTests
{
    [Fact]
    public void Resolve_RejectsAnEmptyCallerPath()
    {
        // [CallerFilePath] not being populated means the gate was invoked in a way that cannot
        // resolve a repo; that has to be loud rather than resolving to something arbitrary.
        var ex = Assert.Throws<InvalidOperationException>(() => RepoRoot.Resolve(""));
        Assert.Contains("CallerFilePath", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_FindsTheNearestSolutionFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "qa-gate-rr-" + Guid.NewGuid().ToString("N")[..8]);
        var nested = Path.Combine(root, "tests", "SomeTests");
        Directory.CreateDirectory(nested);
        try
        {
            File.WriteAllText(Path.Combine(root, "whatever.slnx"), "<Solution/>");
            var caller = Path.Combine(nested, "SomeTests.cs");
            File.WriteAllText(caller, "// test");

            Assert.Equal(root, RepoRoot.Resolve(caller));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Resolve_ThrowsWhenThereIsNoSolutionAbove()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qa-gate-rr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            // A temp folder has no .sln/.slnx anywhere up to the drive root.
            Assert.Throws<InvalidOperationException>(() => RepoRoot.Resolve(Path.Combine(dir, "X.cs")));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }
}
