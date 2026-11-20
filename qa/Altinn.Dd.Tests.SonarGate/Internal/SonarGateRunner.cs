using Altinn.Dd.Tests.Shared;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Dd.Tests.SonarGate.Internal;

internal sealed class SonarGateRunner
{
    // Throwaway-container password; lives only in the SonarQube TestContainer. Long + complex enough
    // to satisfy any reasonable complexity rule (SonarQube can silently refuse weak passwords by
    // returning 2xx without actually applying them).
    // A saved database keeps it (as SonarQube's hash), and a restored container is only usable if it
    // still matches, so changing it makes the next run of every project start from an empty database.
    private const string AdminPassword = "QaTests-Admin-Token-Pw-1!";
    private const string ScannerToolPackage = "dotnet-sonarscanner";
    private const string ScannerToolVersion = "11.2.1";
    private static readonly TimeSpan ComputeTaskTimeout = TimeSpan.FromMinutes(3);
    // A fresh container is up in about a minute; a restored database also rebuilds its search
    // indexes first. A damaged one may never come up, so the wait has to end for the next candidate.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    private readonly SonarGateOptions _options;
    private readonly string _consumerCallerPath;
    private readonly Logger _logger;
    private readonly ProcessRunner _process;

    public SonarGateRunner(SonarGateOptions options, ITestOutputHelper output, string consumerCallerPath)
    {
        _options = options;
        _consumerCallerPath = consumerCallerPath;
        _logger = new Logger(output, "SonarGate");
        _process = new ProcessRunner(_logger, msg => new SonarGateException(msg));
    }

    public async Task RunAsync()
    {
        Skip.IfNot(
            string.Equals(Environment.GetEnvironmentVariable("QATESTS"), "1", StringComparison.Ordinal),
            "Set QATESTS=1 to enable this opt-in code-quality test.");

        Skip.IfNot(await SonarQubeApi.DockerIsAvailable(), "Docker is not available on this host.");

        var repoRoot = RepoRoot.Resolve(_consumerCallerPath);
        // The consumer's test file lives in their QaTests/ folder (or whatever they named it); we
        // derive local working paths from there so we don't have to assume a folder name.
        var qaTestsDir = Path.GetDirectoryName(_consumerCallerPath)!;
        var scanCsproj = Path.Combine(repoRoot, _options.ScanCsprojRelativePath);
        var historyDir = Path.Combine(qaTestsDir, "history");

        // Durable run ledger lives in blob — the local history/ folder on a CI runner is only
        // whatever the checkout carried, so it isn't authoritative. Created here (not at archive
        // time) so the unchanged-HEAD skip below can consult it before we pay for the container.
        var blobContainer = BlobAuth.CreateContainerClient(_options.BlobStorageAccount, _options.BlobContainer);
        var archiver = new BlobArchiver(_options.ProjectKey, blobContainer, historyDir, _logger);

        // Daily-cron trigger means "no changes" == HEAD hasn't moved since the last archived run;
        // re-scanning the same commit yields an identical gate, snapshot, and dashboard. A null on
        // either side (first run, legacy snapshot, git/blob unavailable) never skips.
        // HEAD says nothing about uncommitted edits, so QATESTS_FORCE=1 runs anyway - the scan's
        // counterpart to QATESTS_COVERAGE=force.
        var force = string.Equals(
            Environment.GetEnvironmentVariable("QATESTS_FORCE"), "1", StringComparison.Ordinal);
        var headSha = await GitHead.ResolveAsync(repoRoot);
        var lastSha = force ? null : await archiver.GetLastArchivedCommitShaAsync();
        Skip.If(
            headSha is not null && string.Equals(headSha, lastSha, StringComparison.Ordinal),
            $"No commits since last QA run ({headSha?[..7]}); result would be identical. "
            + "Set QATESTS_FORCE=1 to run anyway.");

        var scannerExe = await EnsureSonarScannerAsync(qaTestsDir);

        // The saved database needs a commit to version the analysis by; without one, "new code"
        // could not be tied to "since the last run", so such a run neither restores nor saves.
        var databaseStore = _options.KeepSonarDatabase && headSha is not null
            ? new SonarDatabaseStore(
                _options.ProjectKey,
                _options.SonarQubeImage,
                blobContainer,
                Path.Combine(qaTestsDir, "TestResults", "sonar-db", _options.ProjectKey),
                _logger)
            : null;
        IReadOnlyList<string> savedDatabases =
            databaseStore is null ? [] : await databaseStore.CandidatesAsync();
        if (databaseStore is not null && savedDatabases.Count == 0)
            _logger.Log("No saved SonarQube database; starting empty, so this run has no new code to check.");

        var (sonarqube, sonarClient) = await StartSonarQubeAsync(databaseStore, savedDatabases);
        await using var disposeContainer = sonarqube;
        using var disposeClient = sonarClient;
        var sonarHostUrl = sonarClient.BaseAddress!.ToString().TrimEnd('/');

        _logger.Log($"Setting the quality gate ({SonarQubeApi.QualityGateName}: new bugs and vulnerabilities)…");
        await SonarQubeApi.EnsureQualityGateAsync(sonarClient);

        // Optional coverage step runs BEFORE the scanner window so the report exists when we pass
        // its path to `sonarscanner begin`, and so the test project's compilation isn't analyzed.
        IReadOnlyList<string> coverageReports = [];
        if (_options.Coverage is not null)
        {
            var forceCoverage = string.Equals(
                Environment.GetEnvironmentVariable("QATESTS_COVERAGE"),
                "force",
                StringComparison.OrdinalIgnoreCase);
            var collector = new CoverageCollector(_options, repoRoot, qaTestsDir, _logger, _process);
            coverageReports = await collector.CollectAsync(forceCoverage);
        }

        _logger.Log("Beginning Sonar scan…");
        // Pass credentials directly to the scanner instead of minting a user token via
        // /api/user_tokens/generate — that endpoint rejected Basic auth on SonarQube 10.6.0-community
        // even after a successful password rotation, while change_password and authentication/validate
        // both accept the same Basic auth header. The scanner accepts sonar.login/sonar.password and
        // uses them for every API call it makes.
        await _process.RunAsync(
            scannerExe, BeginArgs(_options, sonarHostUrl, coverageReports, headSha), qaTestsDir);

        _logger.Log($"Building {Path.GetFileName(scanCsproj)} (the scanner intercepts this build)…");
        await _process.RunAsync("dotnet", $"build \"{scanCsproj}\" --no-incremental", qaTestsDir);

        _logger.Log("Ending Sonar scan and uploading report…");
        await _process.RunAsync(
            scannerExe,
            $"end /d:sonar.login=admin /d:sonar.password={AdminPassword}",
            qaTestsDir);

        var api = new SonarQubeApi(sonarClient, _options.ProjectKey);

        _logger.Log("Waiting for SonarQube compute task to finish…");
        await api.WaitForComputeTaskAsync(ComputeTaskTimeout);

        _logger.Log("Querying quality gate…");
        var gate = await api.FetchQualityGateAsync();
        _logger.Log($"Quality gate: {gate.Status}. Conditions: {gate.ConditionsSummary}");

        _logger.Log("Fetching headline metrics for history snapshot…");
        var metrics = await api.FetchMetricsAsync();

        _logger.Log("Fetching top issues and hotspots…");
        var top = await api.FetchTopFindingsAsync();

        var historyWriter = new HistoryWriter(historyDir, _options.ProjectKey);
        var snapshotFile = historyWriter.WriteSnapshot(gate, metrics, top, headSha);
        _logger.Log($"Wrote {snapshotFile}");
        historyWriter.RegenerateSummary();
        _logger.Log("Refreshed history/summary.md");

        // Archive the snapshot + summary to blob (durable history; private "reports" container).
        // The container client + archiver were created up-front so the unchanged-HEAD skip could
        // read the ledger. The cross-project dashboard is rendered separately by oed-admin from
        // these snapshots — the package no longer builds or publishes any HTML.
        await archiver.ArchiveAsync(snapshotFile);

        if (gate.Status == "OK" && databaseStore is not null)
            await SaveDatabaseAsync(sonarqube, databaseStore, headSha!);

        if (gate.Status != "OK")
            throw new SonarGateException(
                $"SonarQube quality gate failed. Conditions: {gate.ConditionsSummary}");
    }

    /// <summary>
    /// Starts SonarQube from the first saved database that works, falling back to an empty one.
    /// A saved database that fails to download, start, or pass
    /// <see cref="SonarQubeApi.RestoredDatabaseIsUsableAsync"/> is logged and skipped; only a
    /// failure with an empty database fails the run.
    /// </summary>
    private async Task<(IContainer Container, HttpClient Client)> StartSonarQubeAsync(
        SonarDatabaseStore? store, IReadOnlyList<string> savedDatabases)
    {
        foreach (var saved in savedDatabases)
        {
            try
            {
                _logger.Log($"Restoring SonarQube database from {saved}…");
                var file = await store!.DownloadAsync(saved);
                var started = await StartContainerAsync(file);
                var api = new SonarQubeApi(started.Client, _options.ProjectKey);
                if (await api.RestoredDatabaseIsUsableAsync(AdminPassword))
                    return started;

                _logger.Log(
                    $"The database from {saved} started but has no earlier analysis or admin login; "
                    + "trying the next one.");
                started.Client.Dispose();
                await started.Container.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"Could not use the database from {saved} (trying the next one): {ex.Message}");
            }
        }

        if (savedDatabases.Count > 0)
            _logger.Log(
                "No saved SonarQube database was usable; starting empty, so this run has no new code to check.");
        var fresh = await StartContainerAsync(databaseFile: null);
        _logger.Log("Rotating admin password and validating new credentials…");
        await SonarQubeApi.RotateAdminPasswordAsync(fresh.Client, AdminPassword);
        SonarQubeApi.SetBasicAuth(fresh.Client, "admin", AdminPassword);
        return fresh;
    }

    private async Task<(IContainer Container, HttpClient Client)> StartContainerAsync(string? databaseFile)
    {
        _logger.Log($"Starting SonarQube container ({_options.SonarQubeImage})…");
        var builder = new ContainerBuilder(_options.SonarQubeImage)
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/api/system/status")
                    .ForPort(9000)
                    .ForResponseMessageMatching(async m =>
                    {
                        var body = await m.Content.ReadAsStringAsync();
                        return body.Contains("\"status\":\"UP\"");
                    })));
        // Owned by the user SonarQube runs as: a database it cannot write is opened read-only, and
        // SonarQube then stops during startup.
        if (databaseFile is not null)
            builder = builder.WithResourceMapping(
                new FileInfo(databaseFile),
                new FileInfo(SonarDatabaseStore.ContainerDatabasePath),
                SonarDatabaseStore.SonarQubeUserId,
                0,
                UnixFileModes.UserRead | UnixFileModes.UserWrite
                | UnixFileModes.GroupRead | UnixFileModes.GroupWrite);

        var container = builder.Build();
        try
        {
            using var cts = new CancellationTokenSource(StartupTimeout);
            await container.StartAsync(cts.Token);
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }

        var sonarHostUrl = $"http://{container.Hostname}:{container.GetMappedPublicPort(9000)}";
        _logger.Log($"SonarQube UP at {sonarHostUrl}");
        return (container, new HttpClient { BaseAddress = new Uri(sonarHostUrl) });
    }

    /// <summary>
    /// Stops SonarQube so its database is closed cleanly, then saves it. Non-fatal: the gate result
    /// stands either way, and the next run falls back to an older copy.
    /// </summary>
    private async Task SaveDatabaseAsync(IContainer sonarqube, SonarDatabaseStore store, string commitSha)
    {
        try
        {
            _logger.Log("Stopping SonarQube to save its database…");
            await sonarqube.StopAsync();
            var database = await sonarqube.ReadFileAsync(SonarDatabaseStore.ContainerDatabasePath);
            await store.SaveAsync(database, commitSha);
        }
        catch (Exception ex)
        {
            _logger.Log($"Saving the SonarQube database failed (non-fatal): {ex.Message}");
        }
    }

    internal static string BeginArgs(
        SonarGateOptions options,
        string sonarHostUrl,
        IReadOnlyList<string> coverageReports,
        string? commitSha = null)
    {
        var beginArgs = new StringBuilder("begin");
        beginArgs.Append($" /k:\"{options.ProjectKey}\"")
            .Append($" /d:sonar.host.url={sonarHostUrl}")
            .Append($" /d:sonar.login=admin /d:sonar.password={AdminPassword}");
        // Comma-separated: a solution target yields one report per test project.
        if (coverageReports.Count > 0)
            beginArgs.Append($" /d:sonar.cs.opencover.reportsPaths=\"{string.Join(",", coverageReports)}\"");
        // The commit is the project version, so a restored database's new-code period ("previous
        // version") starts at the previous run's commit rather than at the first analysis ever.
        if (commitSha is not null)
            beginArgs.Append($" /v:\"{commitSha}\"");
        beginArgs.Append($" /d:sonar.exclusions=\"{options.SonarExclusions}\"");
        if (options.SonarTestExclusions is { } testExclusions)
            beginArgs.Append($" /d:sonar.test.exclusions=\"{testExclusions}\"");
        beginArgs.Append(" /d:sonar.scanner.scanAll=false");
        return beginArgs.ToString();
    }

    // Installs dotnet-sonarscanner to <qaTestsDir>/TestResults/tools/sonarscanner/ on first run.
    // Subsequent runs reuse the existing binary. Lives under TestResults so the standard
    // [Tt]est[Rr]esult*/ gitignore covers it. This is the "consumer doesn't need a tools manifest"
    // path picked in the design Q&A — keeps the package self-contained.
    private async Task<string> EnsureSonarScannerAsync(string qaTestsDir)
    {
        var toolsDir = Path.Combine(qaTestsDir, "TestResults", "tools", "sonarscanner");
        var exeName = OperatingSystem.IsWindows() ? $"{ScannerToolPackage}.exe" : ScannerToolPackage;
        var scannerExe = Path.Combine(toolsDir, exeName);

        if (!File.Exists(scannerExe))
        {
            _logger.Log($"Installing {ScannerToolPackage} {ScannerToolVersion} to {toolsDir}…");
            Directory.CreateDirectory(toolsDir);
            await _process.RunAsync(
                "dotnet",
                $"tool install {ScannerToolPackage} --version {ScannerToolVersion} --tool-path \"{toolsDir}\"");
        }
        return scannerExe;
    }
}
