# Changelog

All notable changes to `Altinn.Dd.Correspondence` are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Releases are cut by pushing a
`correspondence-v<version>` tag.

When preparing a release, add changes under the version being prepared, marked `Unreleased`, and
replace that with the release date when the tag is pushed. Group entries under **Breaking**,
**Added**, **Changed**, **Fixed** and **Removed**, leaving out empty groups. Breaking entries say
what a caller has to change; longer migration notes belong in the README, linked from here.

## [3.0.0] - Unreleased

See [Breaking changes in 3.0.0](README.md#breaking-changes-in-300) for migration examples.

### Breaking

- `SendCorrespondence`, `Search` and `Get` now return a single generic
  `Altinn.Dd.Correspondence.Result<T>`, replacing `Models.CorrespondenceResult`,
  `Features.Search.Result` and `Features.Get.Result`:
  - `SendCorrespondence` returns `Result<ReceiptExternal>`
  - `Search` returns `Result<IEnumerable<Guid>>`
  - `Get` returns `Result<CorrespondenceOverview>`

  `IsSuccess`, `IsFailure`, `Error`, `Success`, `Failure` and `Match` are unchanged.
  `CorrespondenceResult.Receipt` is now `Result<T>.Value`.
- `CorrespondencesRoleType` and `EmailContentType` moved to the `Altinn.Dd.Correspondence.HttpClients`
  namespace. The duplicate copies in `Models` and `Features.Get` are removed. Members and values are
  unchanged, so only the `using` changes. `NotificationDetails.EmailContentType` and the
  `EmailContentType` members of `InitializeCorrespondenceNotification` now use this type.
- The HTTP client now uses the standard resilience pipeline from
  `Microsoft.Extensions.Http.Resilience`, which adds limits 2.2.0 did not have: a 30s timeout per
  attempt, a 100s total timeout, a circuit breaker and a limit of 1000 concurrent requests. A single
  call to Altinn that takes longer than 30s now fails where 2.2.0 waited.
- `CorrespondenceServiceException` is now thrown when the resilience pipeline rejects a request
  (timeout, open circuit or concurrency limit). These cases throw rather than return a failure
  result. Before 3.0.0 nothing threw this exception.
- `DdCorrespondenceDetails.AllowForwarding` is removed. It never had an effect in Altinn 3: the
  Correspondence API has no forwarding flag, so the value was never sent. Delete the assignment.

### Added

- XML documentation for the whole public API.
- README sections on error handling (which HTTP statuses return a failure result and which throw)
  and on the resilience defaults.
- Sending to an existing Dialogporten dialog: `DdCorrespondenceDetails.DialogId` and
  `DdCorrespondenceDetails.TransmissionType`, with the new `Models.TransmissionType` enum. See
  [Sending to an existing dialog](README.md#sending-to-an-existing-dialog).
- `IDdCorrespondenceService.GetDialogId` and `CorrespondenceOverview.DialogId`, to read the dialog a
  correspondence belongs to. A custom implementation of `IDdCorrespondenceService` has to add
  `GetDialogId`.
- `ReferenceType.DialogportenTransmissionType`. Reading a correspondence that carries this
  reference type used to fail to deserialize.

### Changed

- Retries keep the same policy (3 retries on 408, 429, 5xx and transport failures, exponential
  backoff from 2s) and now add jitter. Responses from retried attempts are disposed instead of
  leaking their connection.
- `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Http` are pinned to 10.0.12
  instead of the `10.0.*` wildcard.

### Removed

- The `Polly`, `Microsoft.Extensions.Http.Polly` and `System.ComponentModel.Annotations` package
  dependencies.

## [2.2.0] - 2026-08-27

The entries below were reconstructed from the release tags after the fact, so they are brief.

### Changed

- Updated `Altinn.ApiClients.Maskinporten` to 10.1.0 and `Polly` to 8.7.0. The
  `Microsoft.Extensions.*` references float on `10.0.*`.
- NuGet lockfiles are committed, so transitive advisories are visible.

## [2.1.x] - 2026-02-17 (pre-release)

Covers `2.1.1-alpha` and `2.1.2-alpha`.

### Added

- `IDdCorrespondenceService.Search`, which finds correspondence ids by resource, role, status,
  date range, senders reference or idempotency key.
- `IDdCorrespondenceService.Get`, which returns a `CorrespondenceOverview` for one correspondence.
- The `altinn:correspondence.read` scope, which `Search` and `Get` need.

## [2.0.x] - 2026-01-26 (pre-release)

Covers `2.0.0-alpha` to `2.0.10-alpha`.

### Breaking

- `IDdMessagingService.SendMessage(DdMessageDetails)` is replaced by
  `IDdCorrespondenceService.SendCorrespondence(DdCorrespondenceDetails)`. It returns a
  `CorrespondenceResult` instead of throwing on API rejections.
- Registration is now `AddDdCorrespondenceService`, configured through `DdCorrespondenceOptions`
  (Maskinporten settings, resource id and `ApiEnvironment`). It replaces
  `AddDdMessagingService<TClientDefinition>` and the `Settings` / `IDdNotificationSettings` model.
- `ReceiptExternal` is simplified to the created correspondences and attachment ids, the
  idempotency key and the senders reference, and it now carries the `CorrespondenceId`.

### Added

- Targets .NET 8, 9 and 10.
- Maskinporten authentication with an X.509 certificate as an alternative to a JWK.
- An `AddDdCorrespondenceService` overload that takes options in code instead of a configuration
  section path.
- Validation of the options at startup.

### Changed

- The Maskinporten token is exchanged for an Altinn token, and only the Altinn token is sent to
  the API.

## [1.0.1] - 2025-11-13

### Breaking

- Registration moved from `IHostBuilder.AddDdCorrespondence(settings, accessTokenProvider)` to
  `IServiceCollection.AddDdMessagingService<TClientDefinition>(maskinportenSection,
  correspondenceSection)`. The library now handles Maskinporten authentication itself, so
  `IAccessTokenProvider` and `BearerTokenHandler` are removed.
- Targets .NET 8 only. .NET 9 support was dropped.

### Changed

- Retries are limited to 408, 429, 5xx and transport failures. Other unsuccessful responses are no
  longer retried.

## [1.0.0] - 2025-10-29

- First release: sends correspondence through the Altinn 3 Correspondence API behind the
  `IDdMessagingService` interface carried over from `Altinn.Oed.Messaging`.

[3.0.0]: https://github.com/Altinn/oed-messaging/compare/correspondence-v2.2.0...HEAD
[2.2.0]: https://github.com/Altinn/oed-messaging/compare/correspondence-v2.1.2-alpha...correspondence-v2.2.0
[2.1.x]: https://github.com/Altinn/oed-messaging/compare/correspondence-v2.0.10-alpha...correspondence-v2.1.2-alpha
[2.0.x]: https://github.com/Altinn/oed-messaging/compare/correspondence-v1.0.1...correspondence-v2.0.10-alpha
[1.0.1]: https://github.com/Altinn/oed-messaging/compare/correspondence-v1.0.0...correspondence-v1.0.1
[1.0.0]: https://github.com/Altinn/oed-messaging/releases/tag/correspondence-v1.0.0
