# oed-messaging

Libraries wrapping Altinn 3 correspondence, used for messaging heirs in Digitalt dødsbo.

## Packages

| Package | Contents |
| --- | --- |
| [`Altinn.Dd.Correspondence`](Altinn.Dd.Correspondence/README.md) | Sending, searching and retrieving Altinn 3 correspondence. |
| [`Altinn.Dd.InternalEvents`](Altinn.Dd.InternalEvents/README.md) | Estate event types and resource ids shared across the programme. |

Both are published to nuget.org by the `Release Correspondence Package` and
`Release InternalEvents Package` workflows.

## Usage

Add the package `Altinn.Dd.Correspondence` from nuget.org and register the service in the DI
container. See [the package README](Altinn.Dd.Correspondence/README.md) for configuration, the
full service contract, and a migration guide from the retired `Altinn.Oed.Messaging` package.

`SendDdCorrespondence` is a console application for sending a correspondence by hand. To use it,
copy `SendDdCorrespondence/appsettings.json.template` to `appsettings.json` and populate it.

## Quality gate

`QaTests` runs a SonarQube quality gate over `Altinn.Dd.Correspondence` on a nightly schedule
(`.github/workflows/qa.yaml`). It needs Docker and is opt-in:

```powershell
$env:QATESTS = "1"; dotnet test ./QaTests/QaTests.csproj
```
