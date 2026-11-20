using System.Globalization;
using System.Text;
using Altinn.Dd.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Altinn.Dd.Tests.DependencyGate.Internal;

/// <summary>
/// Orchestrates one dependency audit: locate the repo, ask the SDK what is vulnerable, verify the
/// solution actually spans the repo, record the result, and fail when it should.
/// </summary>
internal sealed class DependencyGateRunner
{
    private readonly DependencyGateOptions _options;
    private readonly Logger _logger;
    private readonly string _consumerCallerPath;

    public DependencyGateRunner(
        DependencyGateOptions options,
        ITestOutputHelper output,
        string consumerCallerPath)
    {
        _options = options;
        _logger = new Logger(output, "DependencyGate");
        _consumerCallerPath = consumerCallerPath;
    }

    public async Task RunAsync()
    {
        Skip.IfNot(
            string.Equals(Environment.GetEnvironmentVariable("QATESTS"), "1", StringComparison.Ordinal),
            "Set QATESTS=1 to enable this opt-in dependency audit.");

        var repoRoot = RepoRoot.Resolve(_consumerCallerPath);
        var testDir = Path.GetDirectoryName(_consumerCallerPath)!;
        var historyDir = Path.Combine(testDir, _options.HistoryDirName);
        _logger.Log($"Repo root: {repoRoot}");

        var solution = ResolveSolution(repoRoot);
        _logger.Log($"Auditing solution: {Path.GetFileName(solution)}");

        // No --force-evaluate and no restore: the QA workflow restores before the test step, and
        // this must report on what is actually resolved rather than re-resolving it. --no-restore
        // is explicit because `dotnet list package` restores implicitly otherwise, and a restore
        // that fails on NU1903/NU1904 lists nothing - before IgnoredAdvisories is ever applied.
        var (exitCode, stdout, stderr) = await ProcessRunner.RunRawAsync(
            "dotnet",
            $"list \"{solution}\" package --vulnerable --include-transitive --format json --no-restore",
            workingDir: repoRoot,
            timeout: TimeSpan.FromMinutes(10));

        // The exit code alone is not a verdict. `dotnet list` returns non-zero when ANY project in
        // the solution cannot be evaluated, while still emitting complete results for the ones it
        // could - and it cannot evaluate an .esproj, which uses packages.config. Two repos here
        // wrap an npm client that way, so treating non-zero as "could not audit" silently skipped
        // the audit for exactly the two repos with the largest dependency graphs.
        //
        // So: judge on whether a usable JSON document came back, and report the problems it lists.
        var json = ExtractJson(stdout);
        Skip.If(
            json is null,
            $"`dotnet list package --vulnerable` produced no JSON document (exit {exitCode}); the "
            + $"audit could not run. Is the repo restored? {FirstLine(stderr, stdout)}");

        var problems = VulnerabilityReport.Problems(json!);
        var unevaluable = problems.Where(p => !p.IsPackagesConfigProject).ToList();
        foreach (var p in problems)
            _logger.Log(
                $"  {(p.IsPackagesConfigProject ? "Not auditable" : "Problem")}: "
                + $"{Path.GetFileName(p.Project)} — {p.Text}");

        var findings = VulnerabilityReport.Parse(json!);
        var rows = DependencyHistoryWriter.Collapse(findings, _options.IgnoredAdvisories);
        var blocking = VulnerabilityReport
            .Blocking(findings, _options.FailAtOrAbove, _options.IgnoredAdvisories);
        var blockingRows = rows
            .Where(r => r.Severity >= _options.FailAtOrAbove && !r.Ignored)
            .ToList();

        foreach (var r in rows)
            _logger.Log(
                $"  {r.Severity,-8} {r.Package} {r.ResolvedVersion} "
                + $"[{(r.Direct ? "direct" : "transitive")}] {r.AdvisoryId} "
                + $"({string.Join(", ", r.Projects)}){(r.Ignored ? " — ignored by configuration" : "")}");

        // A project the SDK cannot evaluate at all is a real gap; one that merely uses
        // packages.config is not, because it holds no PackageReference to audit. The .esproj
        // wrappers here are npm clients whose actual dependencies are their package-lock.json.
        //
        // An incomplete audit still fails on what it did find: a blocking advisory in an evaluable
        // project must not hide behind a skip caused by a different project. With nothing blocking
        // it skips, as before, and records no snapshot - partial counts would read as a clean run.
        if (unevaluable.Count > 0)
        {
            var incomplete =
                $"`dotnet list package --vulnerable` could not evaluate {unevaluable.Count} project(s); "
                + $"the audit is incomplete. First: {unevaluable[0].Text}";
            if (blockingRows.Count > 0)
                throw new DependencyGateException(
                    $"{Describe(blockingRows, blocking.Count)}{Environment.NewLine}{incomplete}");
            Skip.If(true, incomplete);
        }

        // A project the SDK could not evaluate is listed under "projects" as well as "problems",
        // so subtract it: "audited" must mean audited, both in the log and in the snapshot count
        // the dashboard reads.
        var problemProjects = problems
            .Select(p => p.Project.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var covered = VulnerabilityReport.CoveredProjects(json!)
            .Where(p => !problemProjects.Contains(p.Replace('\\', '/')))
            .ToList();
        var orphans = ProjectsOutsideSolution(repoRoot, covered);

        _logger.Log(
            $"Audited {covered.Count} project(s); {findings.Count} advisory instance(s) found.");

        foreach (var orphan in orphans)
            _logger.Log($"  Project outside the solution, not audited: {orphan}");

        // The snapshot's status must match the test result, so it counts the solution-coverage
        // failure as well as blocking advisories. A deliberate opt-out does not fail.
        var failsOnOrphans = orphans.Count > 0 && _options.FailOnProjectsOutsideSolution;

        var writer = new DependencyHistoryWriter(historyDir, _options.ProjectKey);
        var snapshotFile = writer.WriteSnapshot(
            rows, _options.FailAtOrAbove, blockingRows.Count, covered.Count, orphans,
            failed: blockingRows.Count > 0 || failsOnOrphans);
        writer.RegenerateSummary();

        var uploader = new SasBlobUploader(
            _options.BlobStorageAccount, _options.BlobContainer, _logger);
        var prefix = $"{_options.ProjectKey}/history";
        await uploader.TryUploadAsync(
            $"{prefix}/{Path.GetFileName(snapshotFile)}", snapshotFile, "application/json");
        var summary = Path.Combine(historyDir, DependencyHistoryWriter.SummaryFileName);
        if (File.Exists(summary))
            await uploader.TryUploadAsync(
                $"{prefix}/{DependencyHistoryWriter.SummaryFileName}",
                summary,
                "text/markdown; charset=utf-8");

        if (failsOnOrphans)
            throw new DependencyGateException(
                $"{orphans.Count} project(s) are not in {Path.GetFileName(solution)} and were "
                + "therefore not audited. An unaudited project is how an advisory goes unnoticed "
                + "indefinitely — add them to the solution, or set "
                + $"FailOnProjectsOutsideSolution = false if that is deliberate.{Environment.NewLine}"
                + string.Join(Environment.NewLine, orphans.Select(o => $"  {o}")));

        if (blockingRows.Count > 0)
            throw new DependencyGateException(Describe(blockingRows, blocking.Count));

        _logger.Log(
            $"No advisories at or above {_options.FailAtOrAbove}. "
            + $"{rows.Count} finding(s) recorded below the threshold.");
    }

    private string ResolveSolution(string repoRoot)
    {
        if (_options.SolutionRelativePath is { } rel)
        {
            var explicitPath = Path.Combine(repoRoot, rel);
            if (!File.Exists(explicitPath))
                throw new DependencyGateException(
                    $"SolutionRelativePath '{rel}' does not exist under '{repoRoot}'.");
            return explicitPath;
        }

        var solutions = Directory
            .EnumerateFiles(repoRoot)
            .Where(f => Path.GetExtension(f) is ".sln" or ".slnx")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        return solutions.Count switch
        {
            1 => solutions[0],
            0 => throw new DependencyGateException(
                $"No .sln or .slnx found at repo root '{repoRoot}'."),
            _ => throw new DependencyGateException(
                $"{solutions.Count} solutions found at repo root; set SolutionRelativePath to pick "
                + $"one: {string.Join(", ", solutions.Select(Path.GetFileName))}"),
        };
    }

    /// <summary>
    /// Projects present in the working tree but absent from the audited solution. Anything under
    /// bin/ or obj/ is excluded — SDKs generate .csproj files there (the Azure Functions
    /// WorkerExtensions project, for one), and those are build artefacts rather than source.
    /// </summary>
    private static IReadOnlyList<string> ProjectsOutsideSolution(
        string repoRoot,
        IReadOnlyList<string> coveredProjectPaths)
    {
        var covered = coveredProjectPaths
            .Select(Normalise)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Directory
            .EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsBuildArtefact(p, repoRoot))
            .Where(p => !covered.Contains(Normalise(p)))
            .Select(p => Path.GetRelativePath(repoRoot, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        static string Normalise(string path)
            => Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');

        static bool IsBuildArtefact(string path, string root)
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            return relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
        }
    }

    private string Describe(IReadOnlyList<DependencyHistoryWriter.Row> rows, int instanceCount)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"{rows.Count} dependency advisory finding(s) at or above {_options.FailAtOrAbove} ");
        sb.Append(CultureInfo.InvariantCulture, $"({instanceCount} project/package instances):");
        foreach (var r in rows)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"  {r.Severity}: {r.Package} {r.ResolvedVersion} "
                + $"({(r.Direct ? "direct" : "transitive")}) in {string.Join(", ", r.Projects)} — {r.AdvisoryUrl}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// The SDK may print restore or MSBuild noise before the JSON document, so take everything from
    /// the first '{' to the last '}' rather than assuming stdout is pure JSON.
    /// </summary>
    private static string? ExtractJson(string stdout)
    {
        var start = stdout.IndexOf('{');
        var end = stdout.LastIndexOf('}');
        return start >= 0 && end > start ? stdout[start..(end + 1)] : null;
    }

    private static string FirstLine(params string[] candidates)
    {
        foreach (var c in candidates)
        {
            var line = c.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
        }
        return "";
    }
}
