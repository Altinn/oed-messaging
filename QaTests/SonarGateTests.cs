using Altinn.Dd.Tests.SonarGate;
using Xunit;
using Xunit.Abstractions;

namespace QaTests;

// Opt-in SonarQube quality-gate tests for oed-messaging. The actual runner lives in the
// Altinn.Dd.Tests.SonarGate package — this file is just the option blobs. The package source is
// in qa/Altinn.Dd.Tests.SonarGate in this repository.
//
// Two scans, each with its own SonarQube container and ProjectKey:
// - RootProjects: everything in oed-messaging.slnx except qa/ and QaTests/. Coverage comes from
//   Altinn.Dd.Correspondence.Tests. It must not come from oed-messaging.slnx, which contains this
//   project: `dotnet test` on it would start these QA tests again from inside their own run.
// - QaProjects: qa/oed-qa.slnx, the gate packages and their tests, which also supply coverage.
//
// Run with:  $env:QATESTS = "1"; dotnet test ./QaTests/QaTests.csproj
public class SonarGateTests(ITestOutputHelper output)
{
    // SonarGateOptions' default exclusions, which an override replaces rather than extends.
    private const string DefaultExclusions =
        "**/*.js,**/*.jsx,**/*.ts,**/*.tsx,**/*.css,**/*.scss,**/*.html," +
        "**/wwwroot/**,**/node_modules/**," +
        "**/views/Home/Index.cshtml";

    [SkippableFact, Trait("Category", "qa")]
    public Task RootProjects_QualityGate_ReturnsOk() => SonarGate.RunAsync(new()
    {
        ProjectKey = "oed-messaging",
        ScanCsprojRelativePath = "oed-messaging.slnx",
        SonarExclusions = DefaultExclusions + ",**/qa/**,**/QaTests/**",
        Coverage = new()
        {
            TestCsprojRelativePath = "Altinn.Dd.Correspondence.Tests/Altinn.Dd.Correspondence.Tests.csproj",
            // The scan target is a solution at the repo root, which contributes no folder to the
            // default cache key, so name the production project explicitly.
            CoverageInputRoots = ["Altinn.Dd.Correspondence", "Altinn.Dd.Correspondence.Tests"],
        },
    }, output);

    [SkippableFact, Trait("Category", "qa")]
    public Task QaProjects_QualityGate_ReturnsOk() => SonarGate.RunAsync(new()
    {
        ProjectKey = "oed-qa",
        ScanCsprojRelativePath = "qa/oed-qa.slnx",
        Coverage = new() { TestCsprojRelativePath = "qa/oed-qa.slnx" },
    }, output);
}
