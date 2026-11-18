namespace Altinn.Dd.Tests.DependencyGate;

/// <summary>
/// Severity levels as reported by <c>dotnet list package --vulnerable</c>, in the same order NuGet
/// uses. The names match NuGet's own diagnostic codes: NU1901 low, NU1902 moderate, NU1903 high,
/// NU1904 critical.
/// </summary>
public enum AdvisorySeverity
{
    Low = 0,
    Moderate = 1,
    High = 2,
    Critical = 3,
}

/// <summary>
/// Configuration for a <see cref="DependencyGate.RunAsync"/> invocation. Repo-relative paths
/// resolve against the consumer's repo root (the nearest .sln/.slnx above the call site).
/// </summary>
public sealed record DependencyGateOptions
{
    /// <summary>
    /// Project key. Used verbatim as the subfolder under the blob container
    /// (e.g. <c>"oed-app"</c> → <c>reports/oed-app/history/…</c>), so it should match the
    /// <c>ProjectKey</c> the repo's SonarGate test uses — both gates then land in one place.
    /// </summary>
    public required string ProjectKey { get; init; }

    /// <summary>
    /// Fail the test when an advisory at this severity or above is found. Defaults to
    /// <see cref="AdvisorySeverity.High"/>, matching the <c>WarningsAsErrors=NU1903;NU1904</c>
    /// restore gate in every repo's Directory.Build.props — so the test and the build agree about
    /// what is unacceptable. Everything below is still recorded and printed.
    /// </summary>
    public AdvisorySeverity FailAtOrAbove { get; init; } = AdvisorySeverity.High;

    /// <summary>
    /// Repo-relative path of the solution to audit. When null (the default) the runner takes the
    /// single .sln/.slnx at the repo root. Set this only when a repo has more than one.
    /// </summary>
    public string? SolutionRelativePath { get; init; }

    /// <summary>
    /// Advisory URLs to treat as accepted risk — matched by the GHSA id anywhere in the URL, so
    /// <c>"GHSA-xxxx-yyyy-zzzz"</c> is enough. Findings listed here are still recorded in the
    /// snapshot and printed, flagged as ignored; they just do not fail the test.
    ///
    /// Use sparingly and with an expiry in mind: an entry here is a decision that outlives the
    /// person who made it.
    /// </summary>
    public string[] IgnoredAdvisories { get; init; } = [];

    /// <summary>
    /// When true (the default), a project found in the repo but absent from the solution fails the
    /// test. Such a project is invisible to a solution-wide audit, and an unaudited project is how
    /// a critical advisory can sit unreported indefinitely. Set false only for repos that keep
    /// scratch projects out of the solution on purpose.
    /// </summary>
    public bool FailOnProjectsOutsideSolution { get; init; } = true;

    /// <summary>
    /// Azure Storage account hosting the QA blob container. Defaults to the shared DD-wide account,
    /// matching SonarGateOptions.
    /// </summary>
    public string BlobStorageAccount { get; init; } = "oedqa";

    /// <summary>
    /// Blob container where the per-run snapshot is archived. Defaults to <c>"reports"</c>, the
    /// same private container SonarGate writes to.
    /// </summary>
    public string BlobContainer { get; init; } = "reports";

    /// <summary>
    /// Directory for local snapshots, relative to the calling test's folder. Defaults to
    /// <c>"history"</c>, alongside SonarGate's snapshots; the dependency ones are named
    /// <c>deps-*.json</c> so the two never collide.
    /// </summary>
    public string HistoryDirName { get; init; } = "history";
}
