using System.Text.RegularExpressions;
using Xunit;

namespace Altinn.Dd.Tests.Shared.Tests;

/// <summary>
/// HistoryFolder is the piece that keeps two gates writing into one folder from breaking each
/// other. Both failure modes it guards against are silent, so they are pinned here.
/// </summary>
public class HistoryFolderTests : IDisposable
{
    private static readonly Regex Sonar =
        new(@"^\d{8}-\d{6}\.json$", RegexOptions.CultureInvariant);
    private static readonly Regex Deps =
        new(@"^deps-\d{8}-\d{6}\.json$", RegexOptions.CultureInvariant);

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "dd-qa-hf-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryFolderTests()
    {
        Directory.CreateDirectory(_dir);
        Touch("20260101-120000.json");
        Touch("20260102-120000.json");
        Touch("deps-20260101-130000.json");
        Touch("deps-20260103-130000.json");
        Touch("summary.md");
        Touch("deps-summary.md");
        Touch("notes.json");            // a stray file belonging to neither gate
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private void Touch(string name) => File.WriteAllText(Path.Combine(_dir, name), "{}");

    [Fact]
    public void Snapshots_ReturnsOnlyTheMatchingFamily()
    {
        Assert.Equal(
            ["20260101-120000.json", "20260102-120000.json"],
            HistoryFolder.Snapshots(_dir, Sonar).Select(Path.GetFileName).Order());

        Assert.Equal(
            ["deps-20260101-130000.json", "deps-20260103-130000.json"],
            HistoryFolder.Snapshots(_dir, Deps).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Snapshots_ExcludesUnrelatedJson()
        => Assert.DoesNotContain(
            "notes.json",
            HistoryFolder.Snapshots(_dir, Sonar).Select(Path.GetFileName));

    [Fact]
    public void Newest_StaysInsideItsOwnFamily()
    {
        // The trap: "deps-…" sorts after "2026…" ordinally, so an unscoped "newest name wins"
        // comparison hands the sonar caller a dependency snapshot with no matching properties.
        Assert.Equal("20260102-120000.json", Path.GetFileName(HistoryFolder.Newest(_dir, Sonar)));
        Assert.Equal("deps-20260103-130000.json", Path.GetFileName(HistoryFolder.Newest(_dir, Deps)));
    }

    [Fact]
    public void Newest_IsNullWhenTheFamilyIsAbsent()
    {
        var empty = Path.Combine(_dir, "empty");
        Directory.CreateDirectory(empty);
        Assert.Null(HistoryFolder.Newest(empty, Sonar));
    }

    [Fact]
    public void MissingFolder_IsEmptyRatherThanAThrow()
    {
        // A first run has no history/ yet; that is not an error.
        var missing = Path.Combine(_dir, "does-not-exist");
        Assert.Empty(HistoryFolder.Snapshots(missing, Sonar));
        Assert.Null(HistoryFolder.Newest(missing, Sonar));
    }
}
