# Altinn.Dd.InternalEvents

The shared contract for the internal events published inside Digitalt dødsbo: the event type
names, the resource ids they are published under, and the payload classes they carry.

The package is deliberately behaviour-free — no DI registration, no HTTP client, no publisher.
It exists so that a service publishing an event and a service subscribing to it agree on the
type string and the JSON shape without copying either between repositories.

## Install

```bash
dotnet add package Altinn.Dd.InternalEvents
```

Targets `net10.0`. No package dependencies beyond the base class library.

## Event types

`EventType` holds the type strings as `const`, so they can be used in `switch` patterns and
attributes:

| Constant                    | Type string                                                      | Published by       | Payload                          |
| --------------------------- | ---------------------------------------------------------------- | ------------------ | -------------------------------- |
| `CaseStatusUpdateValidated` | `no.altinn.events.digitalt-dodsbo.v1.case-status-update-validated` | `oed-events`       | `EstateCaseUpdatedEvent`         |
| `CaseStatusManuallySynced`  | `no.altinn.events.digitalt-dodsbo.v1.case-status-manually-synced`  | `oed-events`       | `EstateCaseUpdatedEvent`         |
| `DeclarationSubmitted`      | `no.altinn.events.digitalt-dodsbo.v1.declaration-submitted`        | `oed`              | `DeclarationSubmittedEvent`      |
| `DeclarationSubmittedV2`    | `no.altinn.events.digitalt-dodsbo.v2.declaration-submitted`        | `oed`              | `DeclarationSubmittedV2Event`    |
| `DeclarationUnsigned`       | `no.altinn.events.digitalt-dodsbo.v1.declaration-unsigned`         | `oed-declaration`  | `DeclarationUnsignedEvent`       |
| `FregProtectedAddressUpdate`| `no.altinn.events.digitalt-dodsbo.v1.freg-protected-address-update`| `oed-events`       | `FregProtectedAddressUpdateEvent`|

`CaseStatusUpdateValidated` is published for every update received from Domstoladministrasjonen
via *hendelsesliste*; `CaseStatusManuallySynced` carries the same payload but is raised when a
single case is re-synced by a manual trigger. `DeclarationSubmittedV2` is the one-form-per-heir
variant of `DeclarationSubmitted` — the type string, not the payload, is what tells the two apart,
so subscribers must match on the full `v1`/`v2` string.

Nothing in the library binds a type string to its payload class. The pairing above is the
convention the publishers follow; keep the table in step when either side changes.

## Resource ids

`ResourceId` holds the Resource Registry ids the events are published under:

| Constant            | Value                                            |
| ------------------- | ------------------------------------------------ |
| `DdPrivateProbate`  | `urn:altinn:resource:app_digdir_dd-private-probate` |
| `OedDeclaration`    | `urn:altinn:resource:app_digdir_oed-declaration`    |
| `DomstolApi`        | `urn:altinn:resource:dodsbo-domstoladmin-api`       |

## Usage

Publishing — the constants supply the `type` and `source` of the event, whatever transport you
put it on:

```csharp
using Altinn.Dd.InternalEvents;
using Altinn.Dd.InternalEvents.Estate;

var payload = new DeclarationUnsignedEvent
{
    SigneePartyIds = ["50001337", "50001338"],
};

await publisher.PublishAsync(
    type: EventType.DeclarationUnsigned,
    resource: ResourceId.OedDeclaration,
    data: payload);
```

Subscribing — switch on the type string, then deserialise the event data into the matching
payload class:

```csharp
using System.Text.Json;
using Altinn.Dd.InternalEvents;
using Altinn.Dd.InternalEvents.Estate;

switch (cloudEvent.Type)
{
    case EventType.CaseStatusUpdateValidated:
    case EventType.CaseStatusManuallySynced:
        var update = JsonSerializer.Deserialize<EstateCaseUpdatedEvent>(cloudEvent.Data);
        // ...
        break;

    case EventType.DeclarationSubmittedV2:
        var submitted = JsonSerializer.Deserialize<DeclarationSubmittedV2Event>(cloudEvent.Data);
        // ...
        break;
}
```

## Payload notes

- Every payload property carries an explicit `[JsonPropertyName]`, so the wire format does not
  depend on the serialiser's naming policy. Deserialise with `System.Text.Json`; another stack
  (Newtonsoft.Json, for instance) ignores these attributes and will not bind the event.
- `EstateCaseUpdatedEvent` is the largest payload and the only polymorphic one. Both
  `HeirRoleV2` and `ProbateHeir` are abstract with a `type` discriminator, declared through
  `[JsonPolymorphic]`/`[JsonDerivedType]`:

  | Discriminator        | `HeirRoleV2`                | `ProbateHeir`                   |
  | -------------------- | --------------------------- | ------------------------------- |
  | `Person`             | `PersonHeirRole`            | `PersonProbateHeir`             |
  | `PappPerson`         | `PappPersonHeirRole`        | `PappPersonProbateHeir`         |
  | `Organization`       | `OrganizationHeirRole`      | `OrganizationProbateHeir`       |
  | `PappOrganization`   | `PappOrganizationHeirRole`  | `PappOrganizationProbateHeir`   |

  `Papp*` variants describe parties that exist only on paper — they carry a name (and a date of
  birth for people) instead of a national identity number or organisation number.
- `required` *properties* are enforced by `System.Text.Json`: an event missing one fails
  deserialisation with a `JsonException` rather than binding a default. Treat adding a `required`
  property to an existing payload as a breaking change for every subscriber still emitting the
  old shape.
- `DeclarationSubmittedEvent.DaCaseId` is declared as a field rather than a property, and
  `System.Text.Json` skips public fields unless it is told not to. The v1 declaration-submitted
  payload therefore serialises to `{}` and deserialises `DaCaseId` to `null` under default
  options — silently, because `required` is not enforced for fields either. Deserialise it with
  `new JsonSerializerOptions { IncludeFields = true }`, or prefer
  `DeclarationSubmittedV2Event`, which declares the same member as a property and binds
  correctly out of the box.

## Adding an event

1. Add the `const` to `EventType`, keeping the
   `no.altinn.events.digitalt-dodsbo.<version>.<kebab-case-name>` shape and the XML doc naming
   the publishing service.
2. Add the payload class under `Estate/` (or a new folder for a new domain) with
   `[JsonPropertyName]` on every member.
3. Extend the table above.
4. Release a new version, then bump the reference in the consuming repositories.

When a payload changes in a way subscribers cannot absorb, publish a new type string with the
next `v` — as `DeclarationSubmittedV2` does — instead of changing the existing payload in place.
Both versions can then be published until every subscriber has moved.

## Release

Tagging triggers `release-internalevents.yaml`, which builds, packs with debug symbols, and
pushes to nuget.org:

```bash
git tag internalevents-v1.0.0
git push origin internalevents-v1.0.0
```

A manual workflow dispatch builds `1.0.0-preview` and uploads the `.nupkg` as a workflow
artifact without publishing it, which is the way to check a packaging change before tagging.

## License

MIT — see [LICENSE](../LICENSE).
