# Altinn.Dd.Correspondence.Tests

Tests for the `Altinn.Dd.Correspondence` library. The project has `InternalsVisibleTo` access, so
tests construct the internal feature handlers and the generated `AltinnCorrespondenceClient`
directly.

## Test Structure

### Feature handler tests — `Features/`

These hold most of the value: each builds the real handler over a mocked transport
(`TestSupport/HandlerHarness`), so the handler's own logic runs and only the network is faked.

- **SendHandlerTests**: recipient formatting (the `0192:` country-code rule and its pass-through
  cases), notification channel selection, senders-reference defaulting, idempotency key and
  `IgnoreReservation` forwarding, and problem-document translation. Assertions inspect the request
  body the handler actually put on the wire.
- **SearchHandlerTests**: the `Role` and `ResourceId` guard clauses — including that a rejected
  query costs no API call — filter forwarding, and problem-document translation.
- **GetHandlerTests**: the round trip from a correspondence id to a mapped overview, the requested
  URL, and problem-document translation.

### Mapper tests — `Extensions/`

- **MapperTests**: the wire contract to public DTO translation. The mapper is a wall of unchecked
  enum casts, so these pin the cast pairs that would silently drift if a regenerated client
  renumbered either side.

### Enum parity tests — `Models/`

- **EnumParityTests**: the library keeps public copies of several generated enums so the generated
  client can stay internal, then converts with unchecked casts. Such a cast cannot fail at build
  time or at run time, so a renumbered member would silently produce wrong values everywhere. Each
  test compares one public enum against the generated enum it is cast to, member for member.

  `CorrespondencesRoleType` and `EmailContentType` are no longer duplicated — both live only in
  `HttpClients`, so there is nothing to keep in step for those two.

### Resilience tests — `Extensions/`

- **ResilienceTests**: the retry pipeline wired up by `AddDdCorrespondenceService`, driven through
  the real DI stack because the pipeline only exists once the client is registered. Covers that
  transient failures are retried, that retries are bounded, that a 400 is not retried, that
  responses discarded on a retry are disposed, and that a pipeline rejection is translated into
  `CorrespondenceServiceException` with the Polly rejection kept as its inner exception. The
  disposal test is a regression test for the hand-rolled policy this replaced, which leaked a
  connection per retry.

  The backoff is shortened to milliseconds via `ConfigureAll<HttpStandardResilienceOptions>` so the
  suite does not sleep through the real 2s/4s/8s schedule. Retry counts and what gets retried are
  left as the library configures them, since those are what is under test.

### Service tests — `Services/`

- **DdCorrespondenceServiceTests**: `DdCorrespondenceService` is a thin facade over the three
  handlers, so these pin only that wiring — one handler per operation, no crossed wires.
- **DdCorrespondenceServiceIntegrationTests**: resolves the service out of a real host, through
  `AddDdCorrespondenceService` and the Maskinporten handler chain, with HTTP mocked at the primary
  handler. This is what catches a broken DI registration.

### Test support — `TestSupport/`

- **HandlerHarness**: builds a handler over `MockHttpMessageHandler`, records the request the
  handler sent (body, path, query, count), and hands back the deserialized wire contract.
- **MaskinportenStub**: stubs the Maskinporten metadata, token and Altinn exchange calls, and
  generates a throwaway RSA JWK, so tests that need the whole DI stack run without credentials.

## Namespaces

Each folder has its own namespace under `Altinn.Dd.Correspondence.Tests`, which means
`Tests.Features`, `Tests.Models` and `Tests.Extensions` shadow the library namespaces of the same
name. Inside `Tests.Extensions`, a bare `Features.Get.CorrespondenceStatus` resolves to
`Tests.Features` and fails to compile rather than reaching the library.

Where a test needs one of the shadowed namespaces, alias it at the top of the file instead of
writing the reference out in full:

```csharp
using CorrespondenceGet = Altinn.Dd.Correspondence.Features.Get;
using CorrespondenceModels = Altinn.Dd.Correspondence.Models;
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run a single test class
dotnet test --filter "FullyQualifiedName~SendHandlerTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Dependencies

- **xUnit**: testing framework
- **NSubstitute**: substitutes for `IHandler<,>` and `IOptionsMonitor<>`
- **RichardSzalay.MockHttp**: HTTP transport mocking
- **Microsoft.Extensions.Hosting / DependencyInjection / Configuration**: host used by the
  integration test
- **coverlet.collector**: coverage collection, used only by `--collect`

## Notes

- No test makes a real API call; the Maskinporten token exchange is mocked alongside the
  correspondence endpoints in the integration test.
- The integration test generates a throwaway RSA JWK per run, so it needs no credentials.
- These tests run on `net10.0` only, while the library itself also targets `net9.0` and `net8.0`.
