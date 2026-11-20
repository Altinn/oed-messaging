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

    [Fact]
    public void Scan_VersionsTheAnalysisByCommit()
    {
        // The restored database's new-code period is "since the previous version"; without a
        // version per commit it would run from the very first analysis.
        var args = SonarGateRunner.BeginArgs(Options(), "http://localhost:9000", [], "abc1234def");

        Assert.Contains(" /v:\"abc1234def\"", args, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_HasNoVersionWithoutACommit()
        => Assert.DoesNotContain(
            "/v:",
            SonarGateRunner.BeginArgs(Options(), "http://localhost:9000", []),
            StringComparison.Ordinal);

    [Fact]
    public void Database_LatestCopyIsStoredBesideTheHistory_PerImage()
        => Assert.Equal(
            "oed-app/sonar-db/sonarqube_10.6.0-community/sonar.mv.db.gz",
            SonarDatabaseStore.LatestBlob("oed-app", "sonarqube:10.6.0-community"));

    [Fact]
    public void Database_BackupsShareOneTopLevelFolder_PerProjectAndImage()
        // One lifecycle rule on "sonar-db-backups/" expires every project's backups, and cannot
        // match a latest copy.
        => Assert.Equal(
            "sonar-db-backups/oed-app/sonarqube_10.6.0-community",
            SonarDatabaseStore.BackupPrefix("oed-app", "sonarqube:10.6.0-community"));

    [Fact]
    public void Database_BackupIsNamedByTimeAndCommit()
        => Assert.Equal(
            "b/20260102-030405-abc1234.mv.db.gz",
            SonarDatabaseStore.BackupName(
                "b", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "abc1234def5678"));

    [Fact]
    public void Database_TriesTheLatestCopyThenOlderBackups()
    {
        var candidates = SonarDatabaseStore.OrderCandidates(
            "p/sonar.mv.db.gz",
            "sha3",
            [
                ("p/backups/20260101-000000-sha1.mv.db.gz", "sha1"),
                ("p/backups/20260103-000000-sha3.mv.db.gz", "sha3"),
                ("p/backups/20260102-000000-sha2.mv.db.gz", "sha2"),
                ("p/backups/20251231-000000-sha0.mv.db.gz", "sha0"),
            ]);

        // The backup of the latest run is the same database, so it is skipped; then newest first,
        // limited to BackupsToTry.
        Assert.Equal(
            [
                "p/sonar.mv.db.gz",
                "p/backups/20260102-000000-sha2.mv.db.gz",
                "p/backups/20260101-000000-sha1.mv.db.gz",
            ],
            candidates);
    }

    [Fact]
    public void Database_FallsBackToBackupsWhenThereIsNoLatestCopy()
        => Assert.Equal(
            ["p/backups/20260102-000000-sha2.mv.db.gz", "p/backups/20260101-000000-sha1.mv.db.gz"],
            SonarDatabaseStore.OrderCandidates(
                null,
                null,
                [
                    ("p/backups/20260101-000000-sha1.mv.db.gz", "sha1"),
                    ("p/backups/20260102-000000-sha2.mv.db.gz", "sha2"),
                ]));

    [Fact]
    public void Gate_ReplacesSonarWaysConditionsWithBugsAndVulnerabilities()
    {
        // What SonarQube 10.6 gives a newly created gate, plus a wrong threshold on one of ours.
        var (delete, add) = SonarQubeApi.PlanConditions(
        [
            ("1", "new_violations", "GT", "0"),
            ("2", "new_coverage", "LT", "80"),
            ("3", "new_bugs", "GT", "0"),
            ("4", "new_vulnerabilities", "GT", "5"),
        ]);

        Assert.Equal(["1", "2", "4"], delete);
        Assert.Equal([("new_vulnerabilities", "GT", "0")], add);
    }

    [Fact]
    public void Gate_LeavesACorrectGateAlone()
    {
        var (delete, add) = SonarQubeApi.PlanConditions(
        [
            ("3", "new_bugs", "GT", "0"),
            ("4", "new_vulnerabilities", "GT", "0"),
        ]);

        Assert.Empty(delete);
        Assert.Empty(add);
    }

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
