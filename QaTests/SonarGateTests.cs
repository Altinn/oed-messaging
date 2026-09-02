using Altinn.Dd.Tests.SonarGate;
using Xunit;
using Xunit.Abstractions;

namespace QaTests;

// Opt-in SonarQube quality-gate test for oed-messaging. The actual runner lives in the
// Altinn.Dd.Tests.SonarGate package — this file is just the option blob. See
// https://altinn.studio/repos/digdir/dd-qa for the package source.
//
// Scope: Altinn.Dd.Correspondence (the library this repo publishes). The Altinn.Dd.InternalEvents
// library in the same repo isn't scanned by this run — add a separate SonarGate test with its own
// ProjectKey if you want it on the dashboard.
//
// Run with:  $env:QATESTS = "1"; dotnet test ./QaTests/QaTests.csproj
public class SonarGateTests(ITestOutputHelper output)
{
    [SkippableFact, Trait("Category", "qa")]
    public Task QualityGate_ReturnsOk() => SonarGate.RunAsync(new()
    {
        ProjectKey = "oed-messaging",
        ScanCsprojRelativePath = "Altinn.Dd.Correspondence/Altinn.Dd.Correspondence.csproj",
    }, output);
}
