# Altinn.Dd.Tests.DependencyGate

Shared dependency vulnerability gate for the Digitalt Dødsbo programme.

Audits **every project in the consuming repository** for known NuGet advisories, writes a per-run
JSON snapshot to the consumer's `QaTests/history/`, archives it to the same private Azure Blob
container SonarGate uses, and fails the test when anything is found at or above the configured
severity — `High` by default.

## Usage

```csharp
using Altinn.Dd.Tests.DependencyGate;
using Xunit;
using Xunit.Abstractions;

public class DependencyGateTests(ITestOutputHelper output)
{
    [SkippableFact, Trait("Category", "qa")]
    public Task Dependencies_HaveNoHighOrCriticalAdvisories() =>
        DependencyGate.RunAsync(new() { ProjectKey = "my-project" }, output);
}
```

`ProjectKey` should match the one the repo's SonarGate test uses, so both gates archive under the
same `{ProjectKey}/history/` prefix and the dashboard sees them together.

Set `QATESTS=1` to opt in — a plain `dotnet test` skips. Unlike SonarGate this needs no Docker and
no container; a run takes seconds.

## What it audits, and why not just this project

Scope is the whole repository, not the calling test project. A `QaTests` project does not reference
the application projects, so its own dependency graph covers roughly a fifth of a typical repo here
— auditing only that would report a clean result while missing almost everything.

The runner walks up from the calling source file to the repo root (nearest `.sln`/`.slnx`), then
runs one solution-wide `dotnet list package --vulnerable --include-transitive --format json`.

It also checks that every `.csproj` in the working tree is actually **in** that solution. A project
outside the solution is invisible to the audit, and an unaudited project is how an advisory sits
unreported indefinitely. Set `FailOnProjectsOutsideSolution = false` if a repo keeps scratch
projects out on purpose.

## Requirements

The repository must already be restored — the audit reads what restore resolved and deliberately
does not restore or re-evaluate anything itself. The nightly QA workflows already run
`dotnet restore` before `dotnet test`, so this is satisfied there.

If `dotnet list` cannot run at all, the test **skips** with the reason rather than failing. A false
red on an environment problem trains people to ignore the gate.

## Options

| Option | Default | Purpose |
|---|---|---|
| `ProjectKey` | *required* | Blob prefix; match the SonarGate key for the same repo. |
| `FailAtOrAbove` | `High` | Severity that fails the test. Lower severities are still recorded and printed. |
| `SolutionRelativePath` | `null` | Set only when the repo root has more than one solution. |
| `IgnoredAdvisories` | `[]` | Accepted risks, by GHSA id or full URL. Still recorded, flagged `ignored`, but not blocking. |
| `FailOnProjectsOutsideSolution` | `true` | Fail when a project in the tree is not in the solution. |
| `BlobStorageAccount` / `BlobContainer` | `oedqa` / `reports` | Where snapshots are archived. |
| `HistoryDirName` | `history` | Local snapshot folder, relative to the calling test. |

## Relationship to the restore gate

Every repo's `Directory.Build.props` already sets `WarningsAsErrors=NU1903;NU1904`, which fails
`dotnet restore` on a high or critical advisory. This gate deliberately delegates to the same SDK
command over the same advisory data, so the two cannot disagree.

What it adds is the record: the restore gate blocks a build and leaves nothing behind, says nothing
about moderate findings, and produces no trend. This writes a dated snapshot with every severity,
archives it, and puts dependency status on the same dashboard as code quality.

## Footprint

Two package dependencies — `xunit.abstractions` and `Xunit.SkippableFact` — for five transitive
packages in total, against SonarGate's 31. That is deliberate. A package whose job is to keep dependency risk visible
should not be a source of it, and it has to be safe to add to a repository that wants dependency
auditing without SonarQube's Testcontainers/Docker tree.

Blob upload is a plain `PUT` against a container SAS URL rather than `Azure.Storage.Blobs` +
`Azure.Identity`, which would add twelve packages for one write. The consequence is that
`AZURE_STORAGE_SAS_TOKEN` is the only supported credential; without it the snapshot is still
written to disk and the run logs that archiving was skipped.

## Snapshot format

Snapshots are `deps-yyyyMMdd-HHmmss.json`, alongside SonarGate's `yyyyMMdd-HHmmss.json` in the same
folder. Both gates filter to their own file pattern, so neither trips over the other's snapshots.
The summary is `deps-summary.md`.

The blob layout is read by the oed-admin app, so this schema is additive-only.

```json
{
  "timestamp": "2026-08-31T20:14:53.4669020Z",
  "projectKey": "oed-app",
  "gate": { "threshold": "High", "status": "OK", "blocking": 0 },
  "counts": { "critical": 0, "high": 0, "moderate": 2, "low": 0 },
  "projectsAudited": 4,
  "projectsOutsideSolution": [],
  "findings": [
    {
      "severity": "Moderate",
      "package": "OpenTelemetry.Api",
      "resolvedVersion": "1.12.0",
      "direct": false,
      "ignored": false,
      "advisoryId": "GHSA-g94r-2vxg-569j",
      "advisoryUrl": "https://github.com/advisories/GHSA-g94r-2vxg-569j",
      "projects": ["ApiTest", "App", "IntegrationTest"]
    }
  ]
}
```

Findings are one row per package + advisory, with the affected projects named — the same counting
unit the programme's dependency audit uses, so the numbers line up.

## SAS token

Identical to SonarGate's; see the main `README.md` for how to mint `AZURE_STORAGE_SAS_TOKEN` and
store it as an Actions secret. Both gates read the same variable.
