using System.Runtime.CompilerServices;
using Altinn.Dd.Tests.DependencyGate.Internal;
using Xunit.Abstractions;

namespace Altinn.Dd.Tests.DependencyGate;

/// <summary>
/// Public entrypoint for the shared dependency vulnerability gate.
/// </summary>
public static class DependencyGate
{
    /// <summary>
    /// Audit every project in the consuming repository for known NuGet advisories.
    ///
    /// <para>
    /// Internally: probes for QATESTS=1 (calls <c>Skip.IfNot</c> when missing — so the consumer's
    /// test must be a <c>[SkippableFact]</c>); walks up from the call site to the repo root; runs
    /// <c>dotnet list &lt;solution&gt; package --vulnerable --include-transitive --format json</c>;
    /// checks that every .csproj in the repo is actually covered by that solution; writes a
    /// <c>deps-*.json</c> snapshot under <c>history/</c>; archives it to blob storage; and finally
    /// throws when any finding is at or above <see cref="DependencyGateOptions.FailAtOrAbove"/>.
    /// </para>
    ///
    /// <para>
    /// The audit is deliberately delegated to the .NET SDK rather than reimplemented against
    /// NuGet's advisory feed, so its verdict cannot drift from the <c>NU1903</c>/<c>NU1904</c>
    /// restore gate in the repo's Directory.Build.props — both read the same data by the same code
    /// path. It requires the repo to have been restored already, which the QA workflow does before
    /// the test step.
    /// </para>
    ///
    /// <para>
    /// Scope is the whole repository, not the calling test project. A QaTests project does not
    /// reference the application projects, so its own dependency graph covers roughly a fifth of a
    /// typical repo here and would miss almost everything worth finding.
    /// </para>
    ///
    /// <para>
    /// On findings, throws <see cref="DependencyGateException"/> listing them. Blob archive
    /// failures are non-fatal — the audit result is the source of truth.
    /// </para>
    ///
    /// <para>
    /// The <paramref name="consumerCallerPath"/> default captures the consumer's source-file
    /// location via <c>[CallerFilePath]</c> so the runner can walk up to the nearest <c>.sln</c>
    /// and resolve the repo root — no consumer configuration needed.
    /// </para>
    /// </summary>
    /// <param name="options">Per-project configuration (project key, threshold, ignore list).</param>
    /// <param name="output">xUnit test output sink, used for buffered test diagnostics.</param>
    /// <param name="consumerCallerPath">Do not pass — populated by <c>[CallerFilePath]</c>.</param>
    public static Task RunAsync(
        DependencyGateOptions options,
        ITestOutputHelper output,
        [CallerFilePath] string consumerCallerPath = "")
        => new DependencyGateRunner(options, output, consumerCallerPath).RunAsync();
}
