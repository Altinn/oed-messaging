using System.Text.RegularExpressions;

namespace Altinn.Dd.Tests.Shared;

/// <summary>
/// The consumer's <c>history/</c> folder, shared by every gate.
///
/// Each gate writes its own snapshot family there and must read back only its own: SonarGate's
/// <c>yyyyMMdd-HHmmss.json</c> and DependencyGate's <c>deps-yyyyMMdd-HHmmss.json</c> have entirely
/// different shapes. Enumerating plain <c>*.json</c> throws on the first foreign file, and — more
/// insidiously — an ordinal "newest name wins" comparison picks across families, since
/// <c>deps-…</c> sorts after <c>2026…</c>.
///
/// Both gates route their reads through here so that stays true as gates are added.
/// </summary>
internal static class HistoryFolder
{
    /// <summary>Files in <paramref name="historyDir"/> whose name matches <paramref name="pattern"/>.</summary>
    public static IEnumerable<string> Snapshots(string historyDir, Regex pattern)
        => Directory.Exists(historyDir)
            ? Directory.EnumerateFiles(historyDir, "*.json")
                .Where(f => pattern.IsMatch(Path.GetFileName(f)))
            : [];

    /// <summary>
    /// The most recent snapshot by name. Snapshot names are timestamps within a family, so ordinal
    /// name order is chronological order — provided the comparison stays inside one family, which
    /// <paramref name="pattern"/> guarantees.
    /// </summary>
    public static string? Newest(string historyDir, Regex pattern)
        => Snapshots(historyDir, pattern)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .FirstOrDefault();
}
