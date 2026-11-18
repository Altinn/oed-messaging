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

### Step 2 — Generate an account-scoped SAS

The token must cover **both** the private `reports` container (history) and the `$web`
static-website container (dashboard), so it's an account SAS rather than a single-container one:

```powershell
az storage account generate-sas `
  --account-name oedqa `
  --services b `
  --resource-types co `
  --permissions rwl `
  --expiry 2027-06-02 `
  --https-only `
  --account-key "$ACCOUNT_KEY" `
  -o tsv
```

- `--services b` / `--resource-types co` — Blob service, Container + Object level, so the single
  token reaches every container (`reports` and `$web`).
- `--permissions rwl` — read (dashboard regen), write (snapshot + `$web` uploads), list (find
  latest snapshot per project). Deliberately no `d`: a leaked SAS can overwrite, but can't tombstone.
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
    AZURE_STORAGE_SAS_TOKEN: ${{ secrets.AZURE_STORAGE_SAS_TOKEN }}
  run: dotnet test QaTests --no-restore --verbosity normal
```

The test step is the whole job — the runner archives history to `reports` and publishes the
dashboard to `$web` in-process. No separate deploy step or hosting token is needed.
