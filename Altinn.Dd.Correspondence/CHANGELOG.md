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

### Added

- XML documentation for the whole public API.
- README sections on error handling (which HTTP statuses return a failure result and which throw)
  and on the resilience defaults.

### Changed

- Retries keep the same policy (3 retries on 408, 429, 5xx and transport failures, exponential
  backoff from 2s) and now add jitter. Responses from retried attempts are disposed instead of
  leaking their connection.
- `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Http` are pinned to 10.0.12
  instead of the `10.0.*` wildcard.

### Removed

- The `Polly`, `Microsoft.Extensions.Http.Polly` and `System.ComponentModel.Annotations` package
  dependencies.

## [2.2.0]

Changes before 3.0.0 were not recorded in this file.

[3.0.0]: https://github.com/Altinn/oed-messaging/compare/correspondence-v2.2.0...HEAD
[2.2.0]: https://github.com/Altinn/oed-messaging/releases/tag/correspondence-v2.2.0
