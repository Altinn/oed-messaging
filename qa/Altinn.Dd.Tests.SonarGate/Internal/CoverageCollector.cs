using Altinn.Dd.Tests.Shared;
using System.Security.Cryptography;
using System.Text;

namespace Altinn.Dd.Tests.SonarGate.Internal;

/// <summary>
/// Runs the consumer's test project with <c>--collect:"XPlat Code Coverage"</c> and returns the
/// path to the produced <c>coverage.opencover.xml</c>. Cached on a content hash of every input
/// .cs/.csproj file plus the effective runsettings, so repeat runs with unchanged sources reuse
/// the prior report (~30 s saved per iteration).
/// </summary>
internal sealed class CoverageCollector
{
    private readonly Logger _logger;
    private readonly ProcessRunner _process;
    private readonly string _repoRoot;
    private readonly string _qaTestsDir;
    private readonly string _testCsprojAbsolute;
    private readonly string? _runsettingsAbsolute;
    private readonly string[] _excludes;
    private readonly string[] _inputRoots;

    public CoverageCollector(
        SonarGateOptions options,
        string repoRoot,
        string qaTestsDir,
        Logger logger,
        ProcessRunner process)
    {
        if (options.Coverage is null)
            throw new InvalidOperationException(
                "CoverageCollector requires SonarGateOptions.Coverage to be non-null.");

        _logger = logger;
        _process = process;
        _repoRoot = repoRoot;
        _qaTestsDir = qaTestsDir;
        _testCsprojAbsolute = Path.Combine(repoRoot, options.Coverage.TestCsprojRelativePath);
        _runsettingsAbsolute = options.Coverage.RunsettingsRelativePath is { } rel
            ? Path.Combine(repoRoot, rel)
            : null;
        // The option promises the file is used verbatim. Falling back to a generated one would
        // ignore the consumer's collector configuration and change the numbers without a word.
        if (_runsettingsAbsolute is not null && !File.Exists(_runsettingsAbsolute))
            throw new SonarGateException(
                $"Coverage.RunsettingsRelativePath '{options.Coverage.RunsettingsRelativePath}' "
                + $"does not exist under '{repoRoot}'.");
        _excludes = options.Coverage.Excludes;

        // If CoverageInputRoots is empty, derive defaults from the scan target's folder + the test
        // folder. Consumers can extend (or override) when they have additional source roots that
        // affect coverage. A project at the repo root has an empty folder name, which means the
        // repo root itself - dropping it would leave that project's sources out of the cache key
        // and reuse a stale report after they change.
        var inputs = options.Coverage.CoverageInputRoots;
        if (inputs.Length == 0)
        {
            inputs =
            [
                FolderOf(options.ScanCsprojRelativePath),
                FolderOf(options.Coverage.TestCsprojRelativePath),
            ];
        }

        // A root that does not exist would silently drop out of the cache key, so a typo in
        // CoverageInputRoots must fail rather than quietly weaken the cache.
        var missing = inputs.Where(r => !Directory.Exists(Path.Combine(repoRoot, r))).ToList();
        if (missing.Count > 0)
            throw new SonarGateException(
                $"Coverage input root(s) not found under '{repoRoot}': {string.Join(", ", missing)}");

        _inputRoots = inputs
            .Select(r => Path.GetFullPath(Path.Combine(repoRoot, r)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        static string FolderOf(string relativePath)
            => Path.GetDirectoryName(relativePath) is { Length: > 0 } dir ? dir : ".";
    }

    /// <summary>The absolute folders whose sources make up the cache key.</summary>
    internal IReadOnlyList<string> InputRoots => _inputRoots;

    public async Task<string> CollectAsync(bool forceRebuild)
    {
        // Coverage runs BEFORE the scanner window — we don't want the test project's compilation to
        // be analyzed (only the scan target is in scope), and producing the report up front lets us
        // point the scanner at a file that already exists. Output lives under TestResults/<guid>/
        // which the standard `[Tt]est[Rr]esult*/` gitignore covers.
        //
        // Cached on a content hash so subsequent runs reuse the prior report. Content hashing (not
        // mtime) handles auto-generated source files whose mtime updates on every build but whose
        // content rarely changes. Override with QATESTS_COVERAGE=force.
        var coverageDir = Path.Combine(_qaTestsDir, "TestResults");
        Directory.CreateDirectory(coverageDir);

        var cacheKey = ComputeCacheKey();
        _logger.Log($"Coverage cache key: {cacheKey}");

        var coverageFile = forceRebuild ? null : FindCachedCoverageFile(coverageDir, cacheKey);
        if (coverageFile is not null)
        {
            _logger.Log($"Reusing cached coverage report: {coverageFile} (set QATESTS_COVERAGE=force to rebuild)");
            return coverageFile;
        }

        var runsettingsPath = ResolveOrGenerateRunsettings(coverageDir);

        _logger.Log($"Collecting OpenCover coverage from test project into {coverageDir}…");
        await _process.RunAsync(
            "dotnet",
            $"test \"{_testCsprojAbsolute}\" --collect:\"XPlat Code Coverage\" --settings \"{runsettingsPath}\" --results-directory \"{coverageDir}\"",
            _repoRoot);

        coverageFile = Directory
            .EnumerateFiles(coverageDir, "coverage.opencover.xml", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new SonarGateException(
                $"Coverlet did not produce a coverage.opencover.xml under {coverageDir}.");

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(coverageFile)!, "cache-key.txt"), cacheKey);
        _logger.Log($"Coverage report: {coverageFile}");
        return coverageFile;
    }

    // Returns the path of the runsettings file `dotnet test` should consume. When the consumer
    // supplied an explicit path, that file is used verbatim; otherwise we materialize a transient
    // runsettings under TestResults/ with Format=opencover (Sonar's reportsPaths option requires
    // OpenCover, not the default Cobertura) and the configured assembly excludes.
    private string ResolveOrGenerateRunsettings(string coverageDir)
    {
        if (_runsettingsAbsolute is not null)
            return _runsettingsAbsolute;

        var generated = Path.Combine(coverageDir, "coverlet.runsettings");
        File.WriteAllText(generated, BuildGeneratedRunsettingsXml());
        return generated;
    }

    private string BuildGeneratedRunsettingsXml()
    {
        var excludeAttr = string.Join(",", _excludes);
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <DataCollectionRunSettings>
                <DataCollectors>
                  <DataCollector friendlyName="XPlat Code Coverage">
                    <Configuration>
                      <Format>opencover</Format>
                      <Exclude>{excludeAttr}</Exclude>
                    </Configuration>
                  </DataCollector>
                </DataCollectors>
              </DataCollectionRunSettings>
            </RunSettings>
            """;
    }

    private string ComputeCacheKey()
    {
        // Hash the relative path + content of every input file in a stable order. Path-then-content
        // (with a separator byte) prevents collisions where renaming a file to look like another
        // file's content would otherwise hash the same as the original.
        var inputs = _inputRoots
            .SelectMany(EnumerateCoverageInputs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(p => (Absolute: p, Relative: Path.GetRelativePath(_repoRoot, p).Replace('\\', '/')))
            .OrderBy(t => t.Relative, StringComparer.Ordinal)
            .ToList();

        using var sha = SHA1.Create();
        var sep = new byte[] { 0 };
        foreach (var (absolute, relative) in inputs)
        {
            var pathBytes = Encoding.UTF8.GetBytes(relative);
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            sha.TransformBlock(sep, 0, sep.Length, null, 0);
            var content = File.ReadAllBytes(absolute);
            sha.TransformBlock(content, 0, content.Length, null, 0);
            sha.TransformBlock(sep, 0, sep.Length, null, 0);
        }

        // Hash the effective runsettings content (either the explicit consumer file or the XML we'd
        // generate from Excludes). Path is intentionally excluded so cache hits don't depend on
        // where the file lives — only what it contains determines the coverage behavior.
        var settingsContent = _runsettingsAbsolute is not null
            ? File.ReadAllBytes(_runsettingsAbsolute)
            : Encoding.UTF8.GetBytes(BuildGeneratedRunsettingsXml());
        var tag = Encoding.UTF8.GetBytes("runsettings:");
        sha.TransformBlock(tag, 0, tag.Length, null, 0);
        sha.TransformBlock(sep, 0, sep.Length, null, 0);
        sha.TransformBlock(settingsContent, 0, settingsContent.Length, null, 0);
        sha.TransformBlock(sep, 0, sep.Length, null, 0);

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    private static string? FindCachedCoverageFile(string coverageDir, string cacheKey)
    {
        if (!Directory.Exists(coverageDir)) return null;
        foreach (var subdir in Directory.EnumerateDirectories(coverageDir))
        {
            var cacheKeyFile = Path.Combine(subdir, "cache-key.txt");
            var coverageFile = Path.Combine(subdir, "coverage.opencover.xml");
            if (!File.Exists(cacheKeyFile) || !File.Exists(coverageFile)) continue;
            if (string.Equals(File.ReadAllText(cacheKeyFile).Trim(), cacheKey, StringComparison.OrdinalIgnoreCase))
                return coverageFile;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateCoverageInputs(string root)
    {
        foreach (var pattern in new[] { "*.cs", "*.csproj" })
        {
            foreach (var f in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
                yield return f;
            }
        }
    }
}
