using Altinn.Dd.Tests.SonarGate;
using Altinn.Dd.Tests.SonarGate.Internal;
using Xunit;

namespace Altinn.Dd.Tests.DependencyGate.Tests;

/// <summary>
/// SonarGate logic that runs without Docker or blob storage. Lives here because this is the test
/// project that already sees SonarGate's internals.
/// </summary>
public class SonarGateInternalsTests : IDisposable
{
    private readonly string _repo = Path.Combine(
        Path.GetTempPath(), "qa-gate-repo-" + Guid.NewGuid().ToString("N")[..8]);

    public SonarGateInternalsTests() => Directory.CreateDirectory(_repo);

    public void Dispose()
    {
        try { Directory.Delete(_repo, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("oed-app/history/20260101-120000.json", true)]
    [InlineData("oed-app/history/deps-20260101-120000.json", false)]
    [InlineData("oed-app/history/summary.md", false)]
    [InlineData("oed-app/history/deps-summary.md", false)]
    public void Ledger_CountsOnlySonarSnapshots(string blobName, bool expected)
        => Assert.Equal(expected, BlobArchiver.IsSonarSnapshot(blobName));

    [Theory]
    [InlineData("OK", "abc1234")]
    [InlineData("ERROR", null)]
    public void Ledger_ReusesOnlyAPassingRunsCommit(string status, string? expected)
    {
        var json = $$"""{ "commitSha": "abc1234", "qualityGate": { "status": "{{status}}" } }""";
        Assert.Equal(expected, BlobArchiver.CommitShaOfPassingRun(json));
    }

    [Fact]
    public void Ledger_HasNoCommitForALegacySnapshot()
        => Assert.Null(BlobArchiver.CommitShaOfPassingRun("""{ "qualityGate": { "status": "OK" } }"""));

    [Fact]
    public void Coverage_FailsWhenTheExplicitRunsettingsFileIsMissing()
    {
        var options = Options(runsettings: "missing.runsettings");

        var ex = Assert.Throws<SonarGateException>(() => NewCollector(options));
        Assert.Contains("missing.runsettings", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Coverage_FailsWhenAnInputRootIsMissing()
    {
        var options = Options(inputRoots: ["Typo"]);

        var ex = Assert.Throws<SonarGateException>(() => NewCollector(options));
        Assert.Contains("Typo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Coverage_KeysOnTheRepoRootWhenTheScanTargetSitsThere()
    {
        // Path.GetDirectoryName("App.slnx") is "", which used to drop out of the default cache
        // key, so edits to that project's sources reused a stale coverage report.
        Directory.CreateDirectory(Path.Combine(_repo, "App.Tests"));

        var roots = NewCollector(Options()).InputRoots;

        Assert.Contains(Path.GetFullPath(_repo), roots);
        Assert.Contains(Path.GetFullPath(Path.Combine(_repo, "App.Tests")), roots);
    }

    [Fact]
    public void Scan_PassesTestExclusionsWhenSet()
    {
        // sonar.exclusions does not reach test code, so without this a consumer cannot keep a
        // test project out of the scan.
        var args = SonarGateRunner.BeginArgs(
            Options() with { SonarTestExclusions = "**/qa/**" }, "http://localhost:9000", []);

        Assert.Contains(" /d:sonar.test.exclusions=\"**/qa/**\"", args, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_OmitsTestExclusionsByDefault()
        => Assert.DoesNotContain(
            "sonar.test.exclusions",
            SonarGateRunner.BeginArgs(Options(), "http://localhost:9000", []),
            StringComparison.Ordinal);

    private CoverageCollector NewCollector(SonarGateOptions options)
        // The constructor does not log or run anything, so it needs neither collaborator.
        => new(options, _repo, _repo, logger: null!, process: null!);

    private static SonarGateOptions Options(string? runsettings = null, string[]? inputRoots = null)
        => new()
        {
            ProjectKey = "oed-app",
            ScanCsprojRelativePath = "App.slnx",
            Coverage = new()
            {
                TestCsprojRelativePath = "App.Tests/App.Tests.csproj",
                RunsettingsRelativePath = runsettings,
                CoverageInputRoots = inputRoots ?? [],
            },
        };
}
