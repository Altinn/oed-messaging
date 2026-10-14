# Altinn.Dd.Correspondence

A .NET library for sending, searching and retrieving correspondence through the Altinn 3
Correspondence API. This library follows Altinn 3 patterns for HttpClient registration with
Maskinporten authentication.

Targets `net10.0`, `net9.0` and `net8.0`.

See the [changelog](https://github.com/Altinn/oed-messaging/blob/main/Altinn.Dd.Correspondence/CHANGELOG.md)
for what changed in each release.

## Quick Start

### 1. Install Packages

```bash
dotnet add package Altinn.Dd.Correspondence
```

### 2. Configure Settings

Add to your `appsettings.json`:

```json
{
  "DdConfig": {
    "MaskinportenSettings": {
      "ClientId": "your-client-id",
      "Environment": "test",
      "EncodedJwk": "your-base64-encoded-jwk"
    },
    "ResourceId": "oed-correspondence",
    "Environment": "Development"
  }
}
```

**Configuration Details**:
- `MaskinportenSettings.ClientId`: Your Maskinporten client ID (required)
- `MaskinportenSettings.Environment`: Either "test" or "prod" for Maskinporten environment (required)
- `MaskinportenSettings.EncodedJwk`: Base64-encoded JWK of your Maskinporten client's secret
  (required unless you supply `EncodedX509` instead)
- `MaskinportenSettings.EnableDebugLogging`: Optional flag to emit verbose Maskinporten diagnostics
- `ResourceId`: Id of your registered resource in Resource Registry (e.g., "oed-correspondence") (required)
- `Environment`: Environment you target in Altinn 3 — `Development`, `Staging` or `Production`
  (optional, defaults to `Development`)

`Development` and `Staging` both resolve to the Altinn test platform
(`https://platform.tt02.altinn.no`); `Production` resolves to `https://platform.altinn.no`.

**Note**: The scope `"altinn:serviceowner altinn:correspondence.write altinn:correspondence.read"`
is hardcoded in the library — you don't need to specify it. However, your Maskinporten client needs
to have those scopes registered.

### 3. Register Service

In your `Program.cs` or `Startup.cs`:

```csharp
using Altinn.Dd.Correspondence.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// In ConfigureServices or builder.Services
services.AddDdCorrespondenceService("DdConfig");
```

> `AddDdCorrespondenceService` enforces the required correspondence scopes and wires up a
> Maskinporten-enabled `HttpClient` with the standard resilience pipeline. Consumers only need to supply
> environment-specific credentials. Set `EnableDebugLogging` in configuration when troubleshooting.

### 4. Use the Service

Inject `IDdCorrespondenceService` into your classes:

```csharp
public class MyService
{
    private readonly IDdCorrespondenceService _correspondenceService;

    public MyService(IDdCorrespondenceService correspondenceService)
    {
        _correspondenceService = correspondenceService;
    }

    public async Task SendCorrespondenceAsync()
    {
        var messageDetails = new DdCorrespondenceDetails
        {
            Recipient = "123456789", // Organization number
            Title = "Important Notice",
            Summary = "A brief summary",
            Body = "The full message body",
            Sender = "Your Organization",
            Notification = new NotificationDetails
            {
                EmailSubject = "New message in Altinn",
                EmailBody = "You have received a new message.",
                SmsText = "New message in Altinn. Log in to read."
            }
        };

        var result = await _correspondenceService.SendCorrespondence(messageDetails);
    }
}
```

## Features

**What the library does for you:**
- Automatic Maskinporten authentication via `Altinn.ApiClients.Maskinporten`
- Hardcoded scopes for the correspondence API (no need to configure)
- Resilient HTTP client: retries with jittered exponential backoff, timeouts and a circuit breaker
- Automatic organization number formatting
- Idempotency support to prevent duplicate messages

## API Reference

`IDdCorrespondenceService` exposes four operations. Each returns a `CorrespondenceResult<T>`
(namespace `Altinn.Dd.Correspondence.Models`) rather than throwing on an API rejection — see
[Error Handling](#error-handling). Inspect `IsSuccess` /
`IsFailure` and read `Value` or `Error`, or use `Match` to handle both branches in one expression.

### `SendCorrespondence(DdCorrespondenceDetails)` → `CorrespondenceResult<ReceiptExternal>`

Creates a correspondence. Notable fields on `DdCorrespondenceDetails`:

| Field | Notes |
| --- | --- |
| `Recipient` | Organization number or national identity number. A bare 9-digit organization number is automatically prefixed with the `0192:` country code; values already in `urn:altinn:...` or `countrycode:number` form are passed through untouched. |
| `Title`, `Summary`, `Body` | Message content. Title and summary are plain text, body is markdown. |
| `Sender` | Display name shown instead of the organization name in the inbox. |
| `VisibleDateTime` | When the correspondence becomes visible. Defaults to now. |
| `ShipmentDatetime` | When notifications are sent. Defaults to now. |
| `Notification` | Optional. See below. |
| `IgnoreReservation` | Overrides a recipient's KRR reservation against digital communication. |
| `IdempotencyKey` | Generated automatically if not supplied. A generated key only protects the retries within one call; to make a resend after a crash or a queue redelivery safe, persist the key with the work item and set it. |
| `SendersReference` | Defaults to `EXT_DD_SHIP_{IdempotencyKey}` if not supplied. |
| `DialogId` | Optional. The id of an existing Dialogporten dialog; the correspondence is added to it as a transmission instead of creating a new dialog. See [Sending to an existing dialog](#sending-to-an-existing-dialog). |
| `TransmissionType` | Optional, and only valid together with `DialogId` — setting it alone fails locally without calling the API. Defaults to `Information` on Altinn's side. |

The notification channel is derived from which `NotificationDetails` fields you populate:

| Populated | Channel |
| --- | --- |
| `EmailSubject` **and** `EmailBody` | `Email` |
| `SmsText` | `Sms` |
| Both of the above | `EmailAndSms` |
| Neither (or `Notification` is null) | No notification is requested |

On success, `Value` carries the initialized correspondences, the idempotency key and the senders
reference.

### `Search(Query)` → `CorrespondenceResult<IEnumerable<Guid>>`

Returns the ids of matching correspondences. `ResourceId` and `Role` are **required** — a query
missing either fails locally without calling the API. `From`, `To`, `Status`, `OnBehalfOf`,
`SendersReference` and `IdempotencyKey` are optional filters.
`From` and `To` are sent to Altinn in UTC, so a `DateTimeOffset` in any offset selects the window
it names.

`Role` is of type `Altinn.Dd.Correspondence.Models.CorrespondencesRoleType`:

```csharp
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.Models;

var query = new Query(ResourceId: "oed-correspondence", Role: CorrespondencesRoleType.Sender);
```

### `Get(Request)` → `CorrespondenceResult<CorrespondenceOverview>`

Returns a `CorrespondenceOverview` for a single correspondence id, including content, attachments,
notification settings and current status.

### `GetDialogId(Request)` → `CorrespondenceResult<Guid?>`

Returns the id of the Dialogporten dialog a correspondence belongs to — the same value as
`CorrespondenceOverview.DialogId`. It is `null` until Altinn has created the dialog. Altinn does
that in a background job after accepting the send (after the notification order, if there is one)
and publishes the correspondence only once the dialog exists, so a lookup straight after sending
usually returns `null`. Retry until it returns an id, with a deadline: if the dialog cannot be
created, Altinn fails the correspondence and the id stays `null`. `Get` shows that as status
`Failed`.

### Sending to an existing dialog

Every correspondence creates its own dialog in Dialogporten unless it names an existing one. To
keep several correspondences in one dialog, send the first one normally, read its dialog id with
`GetDialogId`, and set `DialogId` on the ones that follow:

```csharp
var dialogResult = await messagingService.GetDialogId(new Request(firstCorrespondenceId));
if (dialogResult.IsFailure)
{
    // The lookup was rejected; dialogResult.Error says why.
    return;
}

if (dialogResult.Value is not { } dialogId)
{
    // No dialog yet. Sending now without a DialogId would create a second dialog, so retry later.
    return;
}

var followUp = new DdCorrespondenceDetails
{
    Recipient = "01010112345",
    Title = "Update",
    Body = "...",
    DialogId = dialogId,
    TransmissionType = TransmissionType.Information
};
```

Check for `null` before sending: a correspondence without a `DialogId` is not an error, it just
creates a new dialog.

Altinn checks that the dialog exists, that it belongs to the same recipient, and that its resource
has the same service owner as the new correspondence's resource; a mismatch is returned as a
failure result. A correspondence sent this way cannot reply to a specific earlier transmission —
Altinn does not support that through Correspondence.

## Complete Example

See the `SendDdCorrespondence` project in this repository for a complete working example.

## Error Handling

An API rejection that carries a problem document is converted into a failure result rather than an
exception — but only for the statuses the generated client documents for that operation:

| Operation | Becomes a failure result | Throws `AltinnCorrespondenceException` |
| --- | --- | --- |
| `SendCorrespondence` | 400, 401, 404, 422 | 409 (duplicate idempotency key) only when the existing correspondence cannot be looked up, anything else |
| `Search` | 400, 401 | anything else, including 404 |
| `Get` | 400, 401, 404 | anything else |

Anything not in the middle column — including a 5xx that outlives the retry policy, and a
transport failure — surfaces as an exception, so keep a try/catch around the call as well as
checking the result. Rejections from the resilience pipeline arrive as
`CorrespondenceServiceException`, a transport failure that outlives the retries arrives as
`HttpRequestException`, and every other status from the API arrives as
`AltinnCorrespondenceException`.

A 409 from `SendCorrespondence` means Altinn already holds a correspondence under that idempotency
key — usually a retry whose first attempt reached Altinn but timed out on the way back, or a caller
resending. Either way the send has happened, so the library looks the correspondence up by the
key and returns a success result with its receipt: its id, current status, recipient and senders
reference, but no notification orders, since those are not part of the lookup. If the lookup finds
nothing, or Altinn or the transport fails it, the 409 throws `AltinnCorrespondenceException` as
before; if the resilience pipeline rejects the lookup, `CorrespondenceServiceException` is thrown.

The lookup goes by the key alone and does not compare content: a key reused for a different
message returns the earlier correspondence's receipt, and the new message is not sent. Use a new
key for every distinct message.

```csharp
try
{
    // Pattern matching example
    var result = await _correspondenceService.SendCorrespondence(messageDetails);
    var message = result.Match(
        onSuccess: receipt => $"Woho {receipt.IdempotencyKey}",
        onFailure: error => $"Buhu {error}");
    Console.WriteLine(message);

    // Without pattern matching
    var result2 = await _correspondenceService.SendCorrespondence(messageDetails);
    if (result2.IsSuccess)
    {
        Console.WriteLine($"Woho {result2.Value!.IdempotencyKey}");
    }
    else if (result2.IsFailure)
    {
        Console.WriteLine($"Buhu {result2.Error}");
    }
}
catch (Exception ex)
{
    // Handle transport errors and the statuses listed above
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Resilience

The client is wrapped in the standard resilience pipeline from
`Microsoft.Extensions.Http.Resilience`, so transient failures are retried and a struggling
endpoint is backed away from rather than hammered.

**Configuration:**
- **Retry count**: 3 additional attempts (initial attempt + 3 retries)
- **Backoff**: exponential from a 2s base, with jitter so parallel callers do not resynchronise
- **Retried**: HTTP 408, 429 and 5xx, plus transport failures and attempt timeouts
- **Attempt timeout**: 30s per try
- **Total request timeout**: 100s, matching the ceiling `HttpClient` applied before 3.0.0
- **Concurrency limit**: 1000 in-flight requests, rejected rather than queued
- **Circuit breaker**: opens when a sustained share of calls fail, and short-circuits while open

**Behaviour:**
- Retries execute transparently without duplicate messages thanks to the API's idempotency keys
- A retry that gets 409 because its first attempt already reached Altinn returns the existing
  correspondence's receipt rather than failing — see [Error Handling](#error-handling)
- `HttpClient.Timeout` is set to infinite, so the timeouts above are the only ones that apply and
  a timeout always surfaces as `CorrespondenceServiceException`
- Responses from retried attempts are disposed, so retrying does not leak connections
- After all retries are exhausted, the last response is handled as described in
  [Error Handling](#error-handling)
- A request that exceeds a timeout, arrives while the breaker is open, or exceeds the concurrency
  limit throws `CorrespondenceServiceException` rather than returning a failure result. The
  underlying Polly rejection is kept as `InnerException` for diagnosis, but callers never have to
  reference Polly to catch it
- Four attempts at 30s exceed the 100s total, so against a persistently slow endpoint the total
  timeout ends the call before the retry budget is spent

**Changing the defaults:** the pipeline reads its settings from the `HttpStandardResilienceOptions`
named `AltinnCorrespondenceClient-standard`. Configure those after `AddDdCorrespondenceService`, so
your values are applied after the library's:

```csharp
services.AddDdCorrespondenceService("DdConfig");
services.Configure<HttpStandardResilienceOptions>("AltinnCorrespondenceClient-standard", options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
    // The pipeline requires the sampling duration to be at least twice the attempt timeout.
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
});
```

## Example Implementation

### SendDdCorrespondence Project

This repository includes a working example project called `SendDdCorrespondence` that demonstrates:

- Full correspondence sending workflow, followed by a search and a get (`dotnet run`)
- Sending to an existing dialog (`dotnet run -- dialog`): sends one correspondence, polls
  `GetDialogId` every 5 seconds for up to 3 minutes until Altinn has created the dialog, then adds
  one transmission of every `TransmissionType` to it

**Key features of the example:**
- Includes proper error handling and logging
- Ready-to-run console application for testing

**To use the example:**
1. Navigate to the `SendDdCorrespondence` project
2. Copy `appsettings.json.template` to `appsettings.json` and configure it with your settings
3. Run `dotnet run` to test correspondence sending

This example serves as both a testing tool and a reference implementation for integrating the
`Altinn.Dd.Correspondence` package into your applications.

## Breaking changes in 3.0.0

This section has migration examples for the breaking changes. The
[changelog](https://github.com/Altinn/oed-messaging/blob/main/Altinn.Dd.Correspondence/CHANGELOG.md)
lists every change in the release.

### The Get overview uses Models.EmailContentType

`Features.Get.EmailContentType` is removed. `EmailContentType` and `ReminderEmailContentType` on
`InitializeCorrespondenceNotification` (the `Notification` of a `CorrespondenceOverview`) now use
`Altinn.Dd.Correspondence.Models.EmailContentType`, the type `NotificationDetails` already used.
Members and values are unchanged, so only code that names the type changes:

```csharp
-if (overview.Notification?.EmailContentType == Altinn.Dd.Correspondence.Features.Get.EmailContentType.Html)
+if (overview.Notification?.EmailContentType == Altinn.Dd.Correspondence.Models.EmailContentType.Html)
```

`CorrespondencesRoleType` and `EmailContentType` stay in `Altinn.Dd.Correspondence.Models`, as in
2.2.0.

### One result type replaces three

`Models.CorrespondenceResult`, `Features.Get.Result` and `Features.Search.Result` were the same
type three times over, differing only in the value they carried. They are replaced by
`Altinn.Dd.Correspondence.Models.CorrespondenceResult<T>`, in the namespace the send result already
lived in:

```csharp
-Task<CorrespondenceResult>     SendCorrespondence(DdCorrespondenceDetails correspondence);
-Task<Features.Search.Result>   Search(Query query);
-Task<Features.Get.Result>      Get(Features.Get.Request request);
+Task<CorrespondenceResult<ReceiptExternal>>        SendCorrespondence(DdCorrespondenceDetails correspondence);
+Task<CorrespondenceResult<IEnumerable<Guid>>>      Search(Query query);
+Task<CorrespondenceResult<CorrespondenceOverview>> Get(Request request);
```

`IsSuccess`, `IsFailure`, `Error`, `Success`, `Failure` and `Match` are unchanged. The one rename
is on the send path: `CorrespondenceResult.Receipt` is now `Value`, matching the other two.

```csharp
-if (result.IsSuccess) Console.WriteLine(result.Receipt!.IdempotencyKey);
+if (result.IsSuccess) Console.WriteLine(result.Value!.IdempotencyKey);
```

The old Search and Get results were records; `CorrespondenceResult<T>` is a sealed class, so value
equality, `with` expressions and the record `ToString` are gone. The constructor of
`DdCorrespondenceService` and the `IHandler<,>` registrations change their type arguments to match,
which only matters if you construct the service yourself instead of using
`AddDdCorrespondenceService`.

### The resilience pipeline can fail calls that used to succeed

The retry policy moved from a hand-rolled Polly handler to the standard pipeline in
`Microsoft.Extensions.Http.Resilience`. Retry counts and backoff are unchanged apart from added
jitter, and responses from retried attempts are now disposed instead of leaking their connection.

The pipeline also adds limits that did not exist before — a per-attempt timeout, a total request
timeout, a circuit breaker and a concurrency limiter. **Before 3.0.0 there was no per-attempt
timeout at all**, so a slow-but-healthy call could take as long as `HttpClient` allowed. The
defaults here are chosen not to fail such calls (30s per attempt, 100s total, matching the old
`HttpClient` ceiling), but a deployment that regularly sees Altinn take longer than 30s for a
single call will now see failures where 2.2.0 waited.

These surface as `CorrespondenceServiceException`, **not** as a failure result — see
[Resilience](#resilience).

### CorrespondenceServiceException is back, with a purpose

It previously existed but was never thrown by anything. It is now what the library raises when the
resilience pipeline rejects a request, so callers do not have to reference Polly to catch a timeout
or an open circuit. Any pre-3.0.0 `catch` block for it was dead code and will now actually catch
something.

### DdCorrespondenceDetails.AllowForwarding is removed

It was an Altinn 2 setting. The Altinn 3 Correspondence API has no forwarding flag and the value
was never sent, so it has done nothing since the move to Altinn 3. Delete the assignment.

### A duplicate idempotency key returns a success

2.2.0 threw `AltinnCorrespondenceException` with status 409 when Altinn already held a
correspondence under the idempotency key. 3.0.0 looks that correspondence up and returns its
receipt as a success; see [Error Handling](#error-handling). A `catch` that used the 409 to detect
duplicates now only sees the cases where the lookup fails.

### IDdCorrespondenceService has a new member

`GetDialogId` is added to the interface. A class that implements `IDdCorrespondenceService` itself,
such as a decorator or a hand-written fake, has to add it. Mocks generated by a mocking library are
not affected.

### Polly is no longer a dependency

The package no longer references `Polly`, `Microsoft.Extensions.Http.Polly` or
`System.ComponentModel.Annotations`. Code that used them through this package has to reference them
directly.

Everything else is additive: Dialogporten dialog support (`DialogId`, `TransmissionType` and
`GetDialogId`), the XML documentation that makes the public surface show up in IntelliSense, and the
fixes listed in the changelog.

## Migration from Altinn.Oed.Messaging

### Overview
This guide covers migrating from `Altinn.Oed.Messaging` to `Altinn.Dd.Correspondence` (Altinn 3).
The new package keeps the same high-level service contract while simplifying the receipt model.

### Prerequisites
- `Altinn.Dd.Correspondence` 3.0.0 or later
- .NET 8.0, 9.0 or 10.0
- Maskinporten integration configured in your app

### Step 1: Update Package References
```xml
<!-- Remove old -->
<!-- <PackageReference Include="Altinn.Oed.Messaging" Version="0.10.2" /> -->

<!-- Add new -->
<PackageReference Include="Altinn.Dd.Correspondence" Version="3.0.0" />
```

### Step 2: Update Service Registration

**Remove old registration:**
```csharp
services.AddSingleton<IOedMessagingService, OedMessagingService>();
services.AddTransient<BearerTokenHandler>();
services.AddHttpClient<IOedMessagingService, OedMessagingService>()
    .AddHttpMessageHandler<BearerTokenHandler>();
```

**Replace with new registration:**
```csharp
// Register correspondence services
services.AddDdCorrespondenceService("DdConfig");
```

### Step 3: Update Configuration

**Old format:**
```json
{
  "AltinnMessagingSettings": {
    "BaseUrl": "https://your-altinn3-url",
    "CorrespondenceSettings": {
      "Sender": "Your Sender Name"
    }
  }
}
```

**New format:**
```json
{
  "DdConfig": {
    "MaskinportenSettings": {
      "ClientId": "my-client-id",
      "Environment": "test",
      "EncodedJwk": "my-encoded-jwk",
      "EnableDebugLogging": false
    },
    "ResourceId": "my-resource-id",
    "Environment": "Development"
  }
}
```

## GitHub Workflow

This package uses an automated GitHub Actions workflow for building and publishing NuGet packages.

### Workflow Triggers

The workflow (`release-correspondence.yaml`) triggers on:

- **Manual dispatch**: You can manually trigger the workflow from the GitHub Actions tab
- **Tagged releases**: When you create a tag matching `correspondence-v*.*` (e.g., `correspondence-v3.0.0`)

### Deployment Process

1. **Development**: Push changes to any branch - this workflow will NOT run
2. **Testing**: Use manual workflow dispatch to test builds without publishing
3. **Release**: Create a tag like `correspondence-v3.0.0` to publish to NuGet.org

### Creating a Release

To publish a new version to NuGet.org:

1. Update `CHANGELOG.md`: move the entries for the version you're releasing under its heading, set
   the release date and point its compare link at the new tag.
2. Create and push the tag:

```bash
# Create and push a tag
git tag correspondence-v3.0.0
git push origin correspondence-v3.0.0
```

The workflow will automatically:
- Build and test the package
- Create NuGet package with debug symbols
- Publish to NuGet.org (requires `NUGET_ORG_API_KEY` secret)

### Workflow Features

- **Target frameworks**: Builds for `net10.0`, `net9.0` and `net8.0`
- **Testing**: Runs all unit and integration tests before packaging
- **Debug symbols**: Includes `.snupkg` files for debugging
- **Artifact upload**: Non-tagged builds create downloadable artifacts
- **Independent deployment**: Only deploys when the Correspondence package is tagged

## Regenerating the API client

`HttpClients/AltinnCorrespondenceClient.cs` is generated by NSwag from
`altinn_correspondence.nswag`, which embeds the Altinn Correspondence OpenAPI document. Nothing in
the build regenerates it — it is a manual step.

NSwag is pinned in the repository's `dotnet-tools.json`. Renovate keeps that pin current, so it can
be newer than the version recorded in the client's header. From the `Altinn.Dd.Correspondence`
folder:

```bash
dotnet tool restore
dotnet nswag run altinn_correspondence.nswag
```

This writes `HttpClients/AltinnCorrespondenceClient.cs` from the embedded document, not from the
URL in the config, so it is reproducible: run against an unchanged document, the only differences
are whitespace and, when the pinned NSwag is newer, the version stamps. To pick up API changes,
replace the
embedded document first, and review the regenerated client on its own rather than alongside a
feature change.

The config sets `useBaseUrl: false`. The client must not carry its own base URL: the environment
comes from `DdCorrespondenceOptions.Environment`, which sets `HttpClient.BaseAddress` in
`HostBuilderExtensions`. A generated `BaseUrl` would default to tt02 and take precedence over it.

The config sets `typeAccessModifier: internal`, so the generated contract stays out of this
package's public surface; the `Models` and `Features` types are the public face, and the library
converts between them and the generated types. The generated file is not edited by hand.

`EnumParityTests` guards every public enum that is a copy of a generated one — run the tests after
regenerating, since an unchecked enum cast cannot report a renumbered member on its own.

## License

Same as parent repository.
