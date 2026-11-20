# Altinn.Dd.Tests.SonarGate

Shared SonarQube quality-gate test runner for the Digitalt Dødsbo programme.

> This package and its sibling live in the oed-messaging repository. `Altinn.Dd.Tests.Shared/` holds
> plumbing compiled into both, and the `Altinn.Dd.Tests.*.Tests` projects hold the tests. The shared
> code is not a package: nothing in it appears in either gate's public API, so publishing it would
> add a dependency to every consumer and a public contract to maintain, for code no consumer can
> call. The other gate is
> **[Altinn.Dd.Tests.DependencyGate](../Altinn.Dd.Tests.DependencyGate/README.md)**, which audits a repo's NuGet
> dependencies for known advisories. They are independent: a repo can take either or both,
> and DependencyGate deliberately carries none of the Testcontainers/Docker tree so it is
> cheap to add to a repo that has no SonarQube test. They are released by the
> `Release SonarGate Package` and `Release DependencyGate Package` workflows.

Spins up SonarQube via [Testcontainers](https://dotnet.testcontainers.org/), scans a target
.NET project, optionally collects coverage from a test project, asserts the quality gate returns
`OK`, writes a per-run JSON snapshot to the consumer's `QaTests/history/`, and archives that
snapshot to a private Azure Blob container. The cross-project dashboard is rendered separately by
the oed-admin app from these snapshots.

## Usage

```csharp
using Altinn.Dd.Tests.SonarGate;
using Xunit;
using Xunit.Abstractions;

public class MyProjectSonarGateTests(ITestOutputHelper output)
{
    [SkippableFact, Trait("Category", "qa")]
    public Task QualityGate_ReturnsOk() => SonarGate.RunAsync(new()
    {
        ProjectKey = "my-project",
        ScanCsprojRelativePath = "MyProject/MyProject.csproj",
        Coverage = new()
        {
            TestCsprojRelativePath = "MyProject.Tests/MyProject.Tests.csproj",
            Excludes = ["[xunit.*]*", "[MyProject.Tests]*"],
        },
    }, output);
}
```

Set `QATESTS=1` to opt in (a plain `dotnet test` skips). Docker must be reachable for the
SonarQube TestContainer.

A run is skipped when HEAD is the commit of the last archived run that passed, because the result
would be identical. HEAD says nothing about uncommitted edits, so set `QATESTS_FORCE=1` to scan
anyway. `QATESTS_COVERAGE=force` likewise rebuilds a cached coverage report.

`SonarExclusions` only excludes main code. The scanner treats the files of a test project as test
code, so keep a test project out of the scan with `SonarTestExclusions` (for example
`"**/QaTests/**"` when the scan target is a solution that contains the QA tests).

## Publish target

Per run, the package **archives** the snapshot JSON + regenerated `summary.md` to an Azure Blob
container under `{ProjectKey}/history/`. Append-only history — every run becomes a new blob, nothing
is overwritten or deleted. The container is private (AAD/SAS-authenticated reads only). The
cross-project dashboard is rendered separately by the oed-admin app, which reads these snapshots.

Defaults:

| Setting              | Default value | Override via                          |
|----------------------|---------------|---------------------------------------|
| `BlobStorageAccount` | `oedqa`       | `SonarGateOptions.BlobStorageAccount` |
| `BlobContainer`      | `reports`     | `SonarGateOptions.BlobContainer`      |

The `reports` container is private. The QA dashboard is rendered from these snapshots inside the
oed-admin app (its backend reads the container via managed identity); there is no public endpoint.


> [!NOTE]
> Publish failures are caught and logged but never fail the test. The SonarQube quality gate is the
source of truth.

## Quality gate

Every scan is judged by a gate named `SonarGate`, set as SonarQube's default: it fails on **new
bugs** or **new vulnerabilities** (`new_bugs > 0`, `new_vulnerabilities > 0`). Sonar way would also
fail on any new code smell, and with no way to mark an issue "won't fix" in the throwaway container,
one minor smell would fail every run until the code changed. Code smells, coverage and duplication
are still measured and archived; they just do not fail the gate.

## Saved SonarQube database

The quality gate only checks new code, and a fresh container has no
earlier analysis to compare with, so on its own every run would pass whatever it found. With
`KeepSonarDatabase` (on by default) the package therefore keeps SonarQube's database in the same
container, beside the history:

| Blob                                                         | Written                         |
|--------------------------------------------------------------|---------------------------------|
| `{ProjectKey}/sonar-db/{image}/sonar.mv.db.gz`               | after every run whose gate passed, replacing the previous one |
| `{ProjectKey}/sonar-db/{image}/backups/{time}-{sha7}.mv.db.gz` | after every run whose gate passed, never replaced |

A run restores the latest copy, scans with the commit SHA as the project version, and so checks
everything since the last successful run. If a copy cannot be downloaded, does not start, or lacks
an earlier analysis of the project, the run tries the two newest older backups and then an empty
database; those fallbacks are logged. A run whose gate fails saves nothing, so its new issues stay
new code until they are fixed.

- The first run, and the first run after `SonarQubeImage` changes, start empty and check nothing.
- Backups are never deleted by the package; a lifecycle rule on the storage account can expire them.
- The database contains the scanned source code. Keep the container private.
- An issue SonarQube gets wrong cannot be marked as such in the throwaway container; suppress it in
  code (`#pragma warning disable` or `// NOSONAR`) instead.

## Consumer dependencies

- `Altinn.Dd.Tests.SonarGate` (this package)
- `xunit` + `xunit.runner.visualstudio` — the test framework
- `Xunit.SkippableFact` — pulled in transitively for the `[SkippableFact]` attribute

Targets `net10.0`.


## SAS token

The `Altinn.Dd.Tests.SonarGate` package authenticates against the `oedqa` storage account with a SAS token.

This token must be present in a repository secret named `AZURE_STORAGE_SAS_TOKEN` for the consuming pipeline.

When the token expires you can regenerate it by performing the following steps:

### Step 1 — Get the storage account key

The SAS is signed by the storage account's primary key. 

Login in your shell using `az login` and choose the `Altinn-DIGDIR-Test` subscription.

Assign the secret to local variable `$ACCOUNT_KEY`:

```powershell
$ACCOUNT_KEY = az storage account keys list `
    --account-name oedqa `
    --query '[0].value' -o tsv
```

### Step 2 — Generate a SAS for the `reports` container

Both gates write only to the private `reports` container, so the token is scoped to that one
container:

```powershell
az storage container generate-sas `
  --account-name oedqa `
  --name reports `
  --permissions rwl `
  --expiry 2027-06-02 `
  --https-only `
  --account-key "$ACCOUNT_KEY" `
  -o tsv
```

- `--name reports` — the archive container. No other container is needed.
- `--permissions rwl` — read (the last snapshot's commit SHA for the unchanged-HEAD skip), write
  (snapshot and summary uploads), list (find the latest snapshot per project). Deliberately no
  `d`: a leaked SAS can write, but can't delete.
- `--expiry` — pick something you can plausibly remember to rotate. 1–2 years balances
  rotation pain against hygiene.
- `--https-only` — refuses plain HTTP. Always on.

Output is a single line starting with `sv=…`. Treat it as a credential — don't paste it into
chat, tickets, or commits.

### Step 3 — Store as a GitHub Actions secret

In each consumer repo:

`Settings → Secrets and variables → Actions → Secrets → New repository secret`

- **Name**: `AZURE_STORAGE_SAS_TOKEN`
- **Value**: paste the SAS from step 2. Leading `?` is optional — `BlobAuth` tolerates
  both forms.

Put it under **Secrets**, not Variables — the SAS is sensitive, and GitHub auto-masks values
pulled from `secrets.*` in workflow logs.

The workflow's test step picks the SAS up via:

```yaml
- name: Run QA tests
  env:
    QATESTS: '1'
    AZURE_STORAGE_SAS_TOKEN: ${{ secrets.AZURE_STORAGE_SAS_TOKEN }}
  run: dotnet test QaTests --no-restore --verbosity normal
```

The test step is the whole job — the runner archives history to `reports` in-process. The
dashboard is rendered separately by the oed-admin app from those snapshots, so there is no deploy
step or hosting token.
