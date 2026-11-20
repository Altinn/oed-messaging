using Altinn.Dd.Tests.DependencyGate.Internal;
using Xunit;
using SonarHistoryWriter = Altinn.Dd.Tests.SonarGate.Internal.HistoryWriter;

namespace Altinn.Dd.Tests.DependencyGate.Tests;

/// <summary>
/// Both gates write into the consumer's <c>history/</c> folder, so the two snapshot families have
/// to coexist. This is the regression that would actually hurt: SonarGate's summary regeneration
/// used to enumerate every <c>*.json</c> there with hardcoded property access, which a
/// <c>deps-*.json</c> would break — and its "latest snapshot" comparison is ordinal, where
/// <c>deps-…</c> sorts after <c>2026…</c>, so a dependency snapshot would have won and silently
/// removed the details section.
/// </summary>
public class HistoryFolderSharingTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "dd-qa-history-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryFolderSharingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // Minimal snapshot in the shape SonarGate writes.
    private const string SonarSnapshot = """
    {
      "timestamp": "2026-01-01T12:00:00.0000000Z",
      "projectKey": "oed-app",
      "commitSha": "abc1234",
      "qualityGate": { "status": "OK", "conditions": [] },
      "metrics": { "bugs": "0", "code_smells": "12", "ncloc": "4200" },
      "top": {
        "bugs": [],
        "codeSmells": [ { "severity": "MINOR", "rule": "csharpsquid:S1118", "component": "App/Program.cs", "line": 4, "message": "Add a private constructor." } ],
        "hotspots": []
      }
    }
    """;

    private void WriteSonarSnapshot(string name) =>
        File.WriteAllText(Path.Combine(_dir, name), SonarSnapshot);

    private string WriteDependencySnapshot(bool failed = false)
    {
        var writer = new DependencyHistoryWriter(_dir, "oed-app");
        return writer.WriteSnapshot(
            rows: [],
            threshold: AdvisorySeverity.High,
            blockingCount: 0,
            projectsAudited: 4,
            projectsOutsideSolution: failed ? ["Scratch/Scratch.csproj"] : [],
            failed: failed);
    }

    [Theory]
    [InlineData(false, "OK")]
    [InlineData(true, "FAILED")]
    public void DependencySnapshot_RecordsTheWholeVerdict_NotJustTheAdvisories(bool failed, string expected)
    {
        // No blocking advisories in either case: a run that fails on projects outside the solution
        // must still be archived as FAILED, or the dashboard shows green for a red test.
        var file = WriteDependencySnapshot(failed);

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal(expected, doc.RootElement.GetProperty("gate").GetProperty("status").GetString());
    }

    [Fact]
    public void SonarSummary_SurvivesADependencySnapshotInTheSameFolder()
    {
        WriteSonarSnapshot("20260101-120000.json");
        WriteDependencySnapshot();

        // Before the filtering fix this threw KeyNotFoundException on the deps file.
        new SonarHistoryWriter(_dir, "oed-app").RegenerateSummary();

        var summary = File.ReadAllText(Path.Combine(_dir, "summary.md"));
        Assert.Contains("2026-01-01 12:00:00", summary, StringComparison.Ordinal);
        Assert.Contains("4200", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SonarSummary_ListsOnlyItsOwnProject()
    {
        // oed-messaging runs two scans into the same history folder. The other project's run is
        // newer, so without the project filter it would also take over the details section.
        WriteSonarSnapshot("20260101-120000.json");
        File.WriteAllText(
            Path.Combine(_dir, "20260102-120000.json"),
            SonarSnapshot
                .Replace("\"oed-app\"", "\"oed-qa\"", StringComparison.Ordinal)
                .Replace("2026-01-01T12", "2026-01-02T12", StringComparison.Ordinal)
                .Replace("4200", "9900", StringComparison.Ordinal)
                .Replace("Add a private constructor.", "Other project finding.", StringComparison.Ordinal));

        new SonarHistoryWriter(_dir, "oed-app").RegenerateSummary();

        var summary = File.ReadAllText(Path.Combine(_dir, "summary.md"));
        Assert.Contains("4200", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-01-02 12:00:00", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("9900", summary, StringComparison.Ordinal);
        Assert.Contains("Add a private constructor.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Other project finding.", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SonarSummary_StillRendersLatestRunDetails()
    {
        WriteSonarSnapshot("20260101-120000.json");
        WriteDependencySnapshot();

        new SonarHistoryWriter(_dir, "oed-app").RegenerateSummary();

        // 'deps-…' sorts after '2026…' ordinally, so an unfiltered "latest" pick would land on the
        // dependency snapshot, find no `top`, and drop this section entirely.
        var summary = File.ReadAllText(Path.Combine(_dir, "summary.md"));
        Assert.Contains("## Latest run details", summary, StringComparison.Ordinal);
        Assert.Contains("Add a private constructor.", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DependencySummary_IgnoresSonarSnapshots()
    {
        WriteSonarSnapshot("20260101-120000.json");
        WriteDependencySnapshot();

        new DependencyHistoryWriter(_dir, "oed-app").RegenerateSummary();

        var summary = File.ReadAllText(
            Path.Combine(_dir, DependencyHistoryWriter.SummaryFileName));
        Assert.Contains("dependency history", summary, StringComparison.Ordinal);
        // The sonar run must not appear as a dependency row.
        Assert.DoesNotContain("2026-01-01 12:00:00", summary, StringComparison.Ordinal);
        Assert.Contains("_No advisories at any severity._", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTwoGates_WriteSeparateSummaryFiles()
    {
        WriteSonarSnapshot("20260101-120000.json");
        WriteDependencySnapshot();

        new SonarHistoryWriter(_dir, "oed-app").RegenerateSummary();
        new DependencyHistoryWriter(_dir, "oed-app").RegenerateSummary();

        Assert.True(File.Exists(Path.Combine(_dir, "summary.md")));
        Assert.True(File.Exists(Path.Combine(_dir, DependencyHistoryWriter.SummaryFileName)));
    }
}
