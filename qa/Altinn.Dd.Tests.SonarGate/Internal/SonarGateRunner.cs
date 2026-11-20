using Altinn.Dd.Tests.Shared;
using System.Text;
using DotNet.Testcontainers.Builders;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Dd.Tests.SonarGate.Internal;

internal sealed class SonarGateRunner
{
    // Throwaway-container password; lives only in the SonarQube TestContainer. Long + complex enough
    // to satisfy any reasonable complexity rule (SonarQube can silently refuse weak passwords by
    // returning 2xx without actually applying them).
    private const string AdminPassword = "QaTests-Admin-Token-Pw-1!";
    private const string ScannerToolPackage = "dotnet-sonarscanner";
    private const string ScannerToolVersion = "11.2.1";
    private static readonly TimeSpan ComputeTaskTimeout = TimeSpan.FromMinutes(3);

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

        _logger.Log($"Starting SonarQube container ({_options.SonarQubeImage})…");
        await using var sonarqube = new ContainerBuilder(_options.SonarQubeImage)
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/api/system/status")
                    .ForPort(9000)
                    .ForResponseMessageMatching(async m =>
                    {
                        var body = await m.Content.ReadAsStringAsync();
                        return body.Contains("\"status\":\"UP\"");
                    })))
            .Build();

        await sonarqube.StartAsync();

        var sonarHostUrl = $"http://{sonarqube.Hostname}:{sonarqube.GetMappedPublicPort(9000)}";
        _logger.Log($"SonarQube UP at {sonarHostUrl}");
        using var sonarClient = new HttpClient { BaseAddress = new Uri(sonarHostUrl) };

        _logger.Log("Rotating admin password and validating new credentials…");
        await SonarQubeApi.RotateAdminPasswordAsync(sonarClient, AdminPassword);
        SonarQubeApi.SetBasicAuth(sonarClient, "admin", AdminPassword);

        // Optional coverage step runs BEFORE the scanner window so the report exists when we pass
        // its path to `sonarscanner begin`, and so the test project's compilation isn't analyzed.
        string? coverageFile = null;
        if (_options.Coverage is not null)
        {
            var forceCoverage = string.Equals(
                Environment.GetEnvironmentVariable("QATESTS_COVERAGE"),
                "force",
                StringComparison.OrdinalIgnoreCase);
            var collector = new CoverageCollector(_options, repoRoot, qaTestsDir, _logger, _process);
            coverageFile = await collector.CollectAsync(forceCoverage);
        }

        _logger.Log("Beginning Sonar scan…");
        // Pass credentials directly to the scanner instead of minting a user token via
        // /api/user_tokens/generate — that endpoint rejected Basic auth on SonarQube 10.6.0-community
        // even after a successful password rotation, while change_password and authentication/validate
        // both accept the same Basic auth header. The scanner accepts sonar.login/sonar.password and
        // uses them for every API call it makes.
        var beginArgs = new StringBuilder("begin");
        beginArgs.Append($" /k:\"{_options.ProjectKey}\"")
            .Append($" /d:sonar.host.url={sonarHostUrl}")
            .Append($" /d:sonar.login=admin /d:sonar.password={AdminPassword}");
        if (coverageFile is not null)
            beginArgs.Append($" /d:sonar.cs.opencover.reportsPaths=\"{coverageFile}\"");
        beginArgs.Append($" /d:sonar.exclusions=\"{_options.SonarExclusions}\"")
            .Append(" /d:sonar.scanner.scanAll=false");

        await _process.RunAsync(scannerExe, beginArgs.ToString(), qaTestsDir);

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

        if (gate.Status != "OK")
            throw new SonarGateException(
                $"SonarQube quality gate failed. Conditions: {gate.ConditionsSummary}");
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
