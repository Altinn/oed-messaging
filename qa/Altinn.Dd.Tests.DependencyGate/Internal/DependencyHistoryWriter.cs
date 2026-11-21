using Altinn.Dd.Tests.Shared;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Altinn.Dd.Tests.DependencyGate.Internal;

/// <summary>
/// Writes per-run dependency snapshots and regenerates <c>deps-summary.md</c>.
///
/// Deliberately separate from SonarGate's HistoryWriter rather than a method on it. Both write into
/// the same <c>history/</c> folder, but the snapshot shapes are unrelated, and SonarGate's summary
/// regeneration reads every <c>*.json</c> there with hardcoded property access. Snapshots here are
/// named <c>deps-*.json</c> and SonarGate now filters to its own <c>yyyyMMdd-HHmmss.json</c>
/// pattern, so neither gate can trip over the other's files.
///
/// The blob layout is a cross-repo contract — oed-admin's /api/qa reads it — so this schema is
/// additive-only from here on.
/// </summary>
internal sealed class DependencyHistoryWriter
{
    public const string SnapshotPrefix = "deps-";

    /// <summary>
    /// This gate's own snapshots: <c>deps-yyyyMMdd-HHmmss.json</c>. Reads go through
    /// <see cref="HistoryFolder"/> so SonarGate's differently-shaped snapshots in the same folder
    /// are never picked up.
    /// </summary>
    internal static readonly Regex SnapshotNamePattern =
        new(@"^deps-\d{8}-\d{6}\.json$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public const string SummaryFileName = "deps-summary.md";

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _historyDir;
    private readonly string _projectKey;

    public DependencyHistoryWriter(string historyDir, string projectKey)
    {
        _historyDir = historyDir;
        _projectKey = projectKey;
    }

    /// <summary>One row per package + advisory, with the affected projects listed.</summary>
    public sealed record Row(
        AdvisorySeverity Severity,
        string Package,
        string ResolvedVersion,
        bool Direct,
        bool Ignored,
        string AdvisoryId,
        string AdvisoryUrl,
        IReadOnlyList<string> Projects);

    /// <summary>
    /// Collapses raw findings to one row per package + advisory. The same package flagged in
    /// several projects of one repo is a single finding with the projects named — the convention
    /// the estate's dependency audit already uses, so counts here match counts there.
    /// </summary>
    public static IReadOnlyList<Row> Collapse(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<string> ignoredAdvisories)
        => findings
            .GroupBy(f => (f.PackageId, f.ResolvedVersion, f.AdvisoryUrl))
            .Select(g =>
            {
                var first = g.First();
                return new Row(
                    Severity: g.Max(f => f.Severity),
                    Package: first.PackageId,
                    ResolvedVersion: first.ResolvedVersion,
                    Direct: g.Any(f => f.IsDirect),
                    Ignored: VulnerabilityReport.IsIgnored(first, ignoredAdvisories),
                    AdvisoryId: first.AdvisoryId,
                    AdvisoryUrl: first.AdvisoryUrl,
                    Projects: g.Select(f => f.ProjectName)
                               .Distinct(StringComparer.Ordinal)
                               .OrderBy(p => p, StringComparer.Ordinal)
                               .ToList());
            })
            .OrderByDescending(r => r.Severity)
            .ThenBy(r => r.Package, StringComparer.Ordinal)
            .ToList();

    public string WriteSnapshot(
        IReadOnlyList<Row> rows,
        AdvisorySeverity threshold,
        int blockingCount,
        int projectsAudited,
        IReadOnlyList<string> projectsOutsideSolution,
        bool failed)
    {
        Directory.CreateDirectory(_historyDir);
        var ts = DateTime.UtcNow;

        var snapshot = new
        {
            Timestamp = ts.ToString("O", CultureInfo.InvariantCulture),
            ProjectKey = _projectKey,
            Gate = new
            {
                Threshold = threshold.ToString(),
                // The whole verdict, not just the advisories: a run that fails because projects
                // sit outside the solution must not be archived as OK.
                Status = failed ? "FAILED" : "OK",
                Blocking = blockingCount,
            },
            Counts = new
            {
                Critical = rows.Count(r => r.Severity == AdvisorySeverity.Critical),
                High = rows.Count(r => r.Severity == AdvisorySeverity.High),
                Moderate = rows.Count(r => r.Severity == AdvisorySeverity.Moderate),
                Low = rows.Count(r => r.Severity == AdvisorySeverity.Low),
            },
            ProjectsAudited = projectsAudited,
            ProjectsOutsideSolution = projectsOutsideSolution,
            Findings = rows.Select(r => new
            {
                Severity = r.Severity.ToString(),
                r.Package,
                r.ResolvedVersion,
                r.Direct,
                r.Ignored,
                r.AdvisoryId,
                r.AdvisoryUrl,
                r.Projects,
            }),
        };

        var file = Path.Combine(_historyDir, $"{SnapshotPrefix}{ts:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
        return file;
    }

    public void RegenerateSummary()
    {
        var rows = new List<(DateTime Ts, string Status, int Crit, int High, int Mod, int Low)>();
        foreach (var file in HistoryFolder.Snapshots(_historyDir, SnapshotNamePattern))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            if (!root.TryGetProperty("timestamp", out var tsEl)) continue;

            var ts = DateTime.Parse(
                tsEl.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var status = root.TryGetProperty("gate", out var gate)
                         && gate.TryGetProperty("status", out var st)
                ? st.GetString() ?? "?"
                : "?";
            var counts = root.TryGetProperty("counts", out var c) ? c : default;
            rows.Add((ts, status, Count(counts, "critical"), Count(counts, "high"),
                      Count(counts, "moderate"), Count(counts, "low")));
        }
        rows.Sort((a, b) => b.Ts.CompareTo(a.Ts));

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {_projectKey} dependency history");
        sb.AppendLine();
        sb.AppendLine("Auto-generated by `Altinn.Dd.Tests.DependencyGate`. Latest run first, up to 30 shown. ");
        sb.AppendLine("Per-run JSON snapshots (`deps-*.json`) in this folder hold the full history.");
        sb.AppendLine();
        sb.AppendLine("| Timestamp (UTC) | Gate | Critical | High | Moderate | Low |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var (ts, status, crit, high, mod, low) in rows.Take(30))
        {
            sb.Append("| ").Append(ts.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
              .Append(" | ").Append(status)
              .Append(" | ").Append(crit.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(high.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(mod.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(low.ToString(CultureInfo.InvariantCulture))
              .AppendLine(" |");
        }

        AppendLatestFindings(sb);

        File.WriteAllText(Path.Combine(_historyDir, SummaryFileName), sb.ToString());

        static int Count(JsonElement counts, string name)
            => counts.ValueKind == JsonValueKind.Object
               && counts.TryGetProperty(name, out var v)
               && v.TryGetInt32(out var i)
                ? i
                : 0;
    }

    private void AppendLatestFindings(StringBuilder sb)
    {
        var latest = HistoryFolder.Newest(_historyDir, SnapshotNamePattern);
        if (latest is null) return;

        using var doc = JsonDocument.Parse(File.ReadAllText(latest));
        if (!doc.RootElement.TryGetProperty("findings", out var findings)
            || findings.GetArrayLength() == 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Latest run details");
            sb.AppendLine();
            sb.AppendLine("_No advisories at any severity._");
            return;
        }

        // Heading is intentionally timestamp-free so external links can use a stable
        // `#latest-run-details` anchor that survives every regeneration.
        sb.AppendLine();
        sb.AppendLine("## Latest run details");
        sb.AppendLine();
        sb.AppendLine("| Severity | Package | Resolved | Reach | Advisory | Projects |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var f in findings.EnumerateArray())
        {
            var projects = f.TryGetProperty("projects", out var p) && p.ValueKind == JsonValueKind.Array
                ? string.Join(", ", p.EnumerateArray().Select(x => x.GetString()))
                : "";
            var ignored = f.TryGetProperty("ignored", out var ig)
                          && ig.ValueKind == JsonValueKind.True;
            sb.Append("| ").Append(Json.GetStringOrEmpty(f, "severity")).Append(ignored ? " (ignored)" : "")
              .Append(" | ").Append(Markdown.Cell(Json.GetStringOrEmpty(f, "package")))
              .Append(" | ").Append(Markdown.Cell(Json.GetStringOrEmpty(f, "resolvedVersion")))
              .Append(" | ").Append(f.TryGetProperty("direct", out var d) && d.ValueKind == JsonValueKind.True
                  ? "direct" : "transitive")
              .Append(" | ").Append(Markdown.Cell(Json.GetStringOrEmpty(f, "advisoryId")))
              .Append(" | ").Append(Markdown.Cell(projects))
              .AppendLine(" |");
        }
    }
}
