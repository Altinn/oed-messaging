namespace Altinn.Dd.Tests.SonarGate;

/// <summary>
/// Configuration for a <see cref="SonarGate.RunAsync"/> invocation. All repo-relative paths
/// resolve against the consumer's repo root (the nearest .sln/.slnx above the call site).
/// </summary>
public sealed record SonarGateOptions
{
    /// <summary>
    /// SonarQube project key. Also used verbatim as the project subfolder under the publish
    /// container (e.g. <c>"oed-app"</c> → <c>reports/oed-app/history/…</c>).
    /// </summary>
    public required string ProjectKey { get; init; }

    /// <summary>Repo-relative path of the .csproj to scan.</summary>
    public required string ScanCsprojRelativePath { get; init; }

    /// <summary>
    /// Comma-separated Sonar exclusion globs. Default covers JS/TS/CSS/HTML and common frontend
    /// folders so the SonarJS bridge doesn't fire on a backend-only scan, plus the scaffolded
    /// Razor home view. Override per consumer to add project-specific paths
    /// (e.g. <c>**/react/**</c>, <c>**/oed-admin.client/**</c>).
    /// </summary>
    public string SonarExclusions { get; init; } =
        "**/*.js,**/*.jsx,**/*.ts,**/*.tsx,**/*.css,**/*.scss,**/*.html," +
        "**/wwwroot/**,**/node_modules/**," +
        "**/views/Home/Index.cshtml";

    /// <summary>
    /// Comma-separated Sonar exclusion globs for test code. <see cref="SonarExclusions"/> applies
    /// only to main code: the scanner classifies a test project's files as test code, which only
    /// this setting excludes. <c>null</c> (the default) = no test exclusions.
    /// </summary>
    public string? SonarTestExclusions { get; init; }

    /// <summary>
    /// Keep SonarQube's database between runs, in the blob container beside the run history, so the
    /// quality gate's new-code conditions compare with the last successful run. With <c>false</c>
    /// every run starts from an empty database, where there is no new code and the default gate
    /// checks nothing. The database holds the scanned source code. Defaults to <c>true</c>.
    /// </summary>
    public bool KeepSonarDatabase { get; init; } = true;

    /// <summary>Optional coverage configuration. <c>null</c> = no coverage report collected.</summary>
    public CoverageOptions? Coverage { get; init; }

    /// <summary>
    /// Azure Storage account that hosts the QA blob container. Defaults to the shared DD-wide
    /// account; override only for local-dev or programme-isolated setups.
    /// </summary>
    public string BlobStorageAccount { get; init; } = "oedqa";

    /// <summary>
    /// Blob container under <see cref="BlobStorageAccount"/> where the per-run snapshot JSON +
    /// per-project summary.md are archived. Defaults to <c>"reports"</c> and stays private — it
    /// holds the raw history that consumers (e.g. the oed-admin app) read to render a dashboard.
    /// </summary>
    public string BlobContainer { get; init; } = "reports";

    /// <summary>SonarQube container image to spin up. Pinned by default; override to bump.</summary>
    public string SonarQubeImage { get; init; } = "sonarqube:10.6.0-community";
}

/// <summary>
/// Coverage configuration. When set, the runner runs the test .csproj with
/// <c>--collect:"XPlat Code Coverage"</c> before the Sonar scan and feeds the resulting
/// OpenCover XML to the scanner via <c>sonar.cs.opencover.reportsPaths</c>.
/// </summary>
public sealed record CoverageOptions
{
    /// <summary>Repo-relative path of the test .csproj that produces the report.</summary>
    public required string TestCsprojRelativePath { get; init; }

    /// <summary>
    /// Optional repo-relative path to a <c>coverlet.runsettings</c> file. When null (the default)
    /// the runner generates a transient runsettings under <c>TestResults/</c> with
    /// <c>&lt;Format&gt;opencover&lt;/Format&gt;</c> and the assembly excludes from
    /// <see cref="Excludes"/>. Set this for fine-grained control (extra collector configuration,
    /// etc.) and the runner will use it verbatim, ignoring <see cref="Excludes"/>.
    /// </summary>
    public string? RunsettingsRelativePath { get; init; }

    /// <summary>
    /// Coverlet assembly exclude patterns applied when the runner generates the transient
    /// runsettings (i.e. <see cref="RunsettingsRelativePath"/> is null). Format follows coverlet
    /// convention — e.g. <c>"[xunit.*]*"</c>, <c>"[MyTests]*"</c>. The default excludes xunit
    /// so its noise doesn't enter the coverage report.
    /// </summary>
    public string[] Excludes { get; init; } = ["[xunit.*]*"];

    /// <summary>
    /// Repo-relative folders whose <c>.cs</c>/<c>.csproj</c> files contribute to the content-hash
    /// cache key. The coverage step is cached on this hash so subsequent runs with unchanged source
    /// reuse the prior coverage report. Defaults to the scan target's folder and the test folder.
    /// </summary>
    public string[] CoverageInputRoots { get; init; } = [];
}
