using Altinn.Dd.Tests.SonarGate;
using Xunit;
using Xunit.Abstractions;

namespace QaTests;

// Opt-in SonarQube quality-gate test for oed-messaging. The actual runner lives in the
// Altinn.Dd.Tests.SonarGate package — this file is just the option blob. See
// https://altinn.studio/repos/digdir/dd-qa for the package source.
//
// Scope: Altinn.Oed.Messaging (the consuming application). The Altinn.Dd.Correspondence and
// Altinn.Dd.InternalEvents NuGet libraries in the same repo aren't scanned by this run —
// add separate SonarGate tests per library if you want them on the dashboard.
//
// Run with:  $env:QATESTS = "1"; dotnet test ./QaTests/QaTests.csproj
public class SonarGateTests(ITestOutputHelper output)
{
    [SkippableFact, Trait("Category", "qa")]
    public Task QualityGate_ReturnsOk() => SonarGate.RunAsync(new()
    {
        ProjectKey = "oed-messaging",
        ScanCsprojRelativePath = "Altinn.Oed.Messaging/Altinn.Oed.Messaging.csproj",
    }, output);
}
