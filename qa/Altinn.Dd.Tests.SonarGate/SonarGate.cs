using System.Runtime.CompilerServices;
using Altinn.Dd.Tests.SonarGate.Internal;
using Xunit.Abstractions;

namespace Altinn.Dd.Tests.SonarGate;

/// <summary>
/// Public entrypoint for the shared SonarQube quality-gate test runner.
/// </summary>
public static class SonarGate
{
    /// <summary>
    /// Run the full SonarQube quality-gate test against a target .NET project.
    ///
    /// <para>
    /// Internally: probes for QATESTS=1 and Docker (calls <c>Skip.IfNot</c> when missing — so the
    /// consumer's test must be a <c>[SkippableFact]</c>); starts SonarQube via TestContainers;
    /// optionally collects coverage from a test project; runs the scanner against the target
    /// project; polls the SonarQube compute task; fetches the gate, headline metrics, and top-N
    /// findings; writes a per-run JSON snapshot under <c>QaTests/history/</c>; regenerates
    /// <c>summary.md</c>; archives the snapshot and summary to Azure Blob storage; and finally
    /// asserts the gate returned <c>OK</c>.
    /// </para>
    ///
    /// <para>
    /// On gate failure throws <see cref="SonarGateException"/> with the failing conditions in the
    /// message. Blob archive errors are non-fatal — the gate result is the source of truth.
    /// </para>
    ///
    /// <para>
    /// The <paramref name="consumerCallerPath"/> default captures the consumer's source-file
    /// location via <c>[CallerFilePath]</c> so the runner can walk up to the nearest
    /// <c>.sln</c> and resolve the repo root — no consumer configuration needed.
    /// </para>
    /// </summary>
    /// <param name="options">Per-project configuration (project key, scan target, coverage, blob archive).</param>
    /// <param name="output">xUnit test output sink, used for buffered test diagnostics.</param>
    /// <param name="consumerCallerPath">Do not pass — populated by <c>[CallerFilePath]</c>.</param>
    public static Task RunAsync(
        SonarGateOptions options,
        ITestOutputHelper output,
        [CallerFilePath] string consumerCallerPath = "")
        => new SonarGateRunner(options, output, consumerCallerPath).RunAsync();
}
