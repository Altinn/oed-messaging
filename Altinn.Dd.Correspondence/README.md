# Altinn.Dd.Correspondence

A .NET library for sending correspondence through Altinn 3 Correspondence API. This library follows Altinn 3 patterns for HttpClient registration with Maskinporten authentication.

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
- `ClientId`: Your Maskinporten client ID (required)
- `Environment`: Either "test" or "prod" for Maskinporten environment (optional)
- `EncodedJwk`: Base64-encoded JWK of your Maskinporten clients secret (required)
- `EnableDebugLogging`: Optional flag to emit verbose Maskinporten diagnostics (optional)
- `ResourceId`: Id of your registered resource in Resource Registry (e.g., "oed-correspondence") (required)
- `Environment`: Environment you target in Altinn 3 (e.g., "Development", "Staging", "Production") (optional)

**Note**: The scope `"altinn:serviceowner altinn:correspondence.write"` is hardcoded in the library - you don't need to specify it. However, your Maskinporten client needs to have that scope registered.

### 3. Register Service

In your `Program.cs` or `Startup.cs`:

```csharp
using Altinn.Dd.Correspondence.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// In ConfigureServices or builder.Services
services.AddDdCorrespondenceService("DdConfig");
```

> `AddDdCorrespondenceService` enforces the required correspondence scope (`altinn:serviceowner altinn:correspondence.write`) and wires up a Maskinporten-enabled `HttpClient` with Polly-based retries. Consumers only need to supply environment-specific credentials. Set `EnableDebugLogging` in configuration when troubleshooting.

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

        var receipt = await _correspondenceService.SendMessage(messageDetails);
    }
}
```

## Features

**What the library does for you:**
- Automatic Maskinporten authentication via `Altinn.ApiClients.Maskinporten`
- Hardcoded scope for correspondence API (no need to configure)
- Resilient HTTP client with Polly retry policy (exponential backoff)
- Automatic organization number formatting
- Idempotency support to prevent duplicate messages

## Complete Example

See the `SendDdCorrespondence` project in this repository for a complete working example.

## API Reference

## Error Handling

```csharp
try
{
    // Pattern matching example
    var receipt = await _correspondenceService.SendCorrespondence(messageDetails);
    var result = receipt.Match(
        onSuccess: receipt => $"Woho {receipt.IdempotencyKey}",
        onFailure: error => $"Buhu {error}");
    Console.WriteLine(result);

    // Without pattern matching
    var receipt2 = await messagingService.SendCorrespondence(messageDetails);
    if (receipt2.IsSuccess)
    {
        Console.WriteLine($"Woho {receipt2.Receipt!.IdempotencyKey}");
    }
    else if (receipt2.IsFailure)
    {
        Console.WriteLine($"Buhu {receipt2.Error}");
    }
}
catch (Exception ex)
{
    // Handle API errors
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Retry Logic

The service automatically retries failed requests with exponential backoff to handle transient network and API errors.

**Retry Configuration:**
- **Retry count**: 3 additional attempts (initial attempt + 3 retries)
- **Backoff strategy**: Exponential (2s, 4s, 8s delays between retries)
- **Retried exceptions/status codes**:
  - `HttpRequestException`, `TaskCanceledException`, `SocketException`
  - HTTP 408 (Request Timeout)
  - HTTP 429 (Too Many Requests)
  - HTTP 5xx status codes

**Retry Behavior:**
- Retries execute transparently without duplicate messages thanks to the API’s idempotency keys
- After all retries are exhausted, the original exception is surfaced to the caller

## Example Implementation

### SendDdCorrespondence Project

This repository includes a working example project called `SendDdCorrespondence` that demonstrates:

- Full correspondence sending workflow

**Key features of the example:**
- Includes proper error handling and logging
- Ready-to-run console application for testing

**To use the example:**
1. Navigate to the `SendDdCorrespondence` project
2. Configure your `appsettings.json` with your settings
3. Run `dotnet run` to test correspondence sending

This example serves as both a testing tool and a reference implementation for integrating the `Altinn.Dd.Correspondence` package into your applications.

## Migration from Altinn.Oed.Messaging

### Overview
This guide covers migrating from `Altinn.Oed.Messaging` to `Altinn.Dd.Correspondence` (Altinn 3). The new package keeps the same high-level service contract while simplifying the receipt model.

### Prerequisites
- `Altinn.Dd.Correspondence` v2.0.0 (supports net10.0)
- .NET 10.0+
- Maskinporten integration configured in your app

### Step 1: Update Package References
```xml
<!-- Remove old -->
<!-- <PackageReference Include="Altinn.Oed.Messaging" Version="0.10.2" /> -->

<!-- Add new -->
<PackageReference Include="Altinn.Dd.Correspondence" Version="2.0.0" />
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

### Step 4: Update Configuration

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
    "Environment": "development"
  }
}
```

## GitHub Workflow

This package uses an automated GitHub Actions workflow for building and publishing NuGet packages.

### Workflow Triggers

The workflow (`release-correspondence.yaml`) triggers on:

- **Manual dispatch**: You can manually trigger the workflow from the GitHub Actions tab
- **Tagged releases**: When you create a tag starting with `correspondence-v` (e.g., `correspondence-v1.0.0`)

### Deployment Process

1. **Development**: Push changes to any branch - workflow will NOT run
2. **Testing**: Use manual workflow dispatch to test builds without publishing
3. **Release**: Create a tag like `correspondence-v1.0.0` to publish to NuGet.org

### Creating a Release

To publish a new version to NuGet.org:

```bash
# Create and push a tag
git tag correspondence-v1.0.0
git push origin correspondence-v1.0.0
```

The workflow will automatically:
- Build and test the package
- Create NuGet package with debug symbols
- Publish to NuGet.org (requires `NUGET_ORG_API_KEY` secret)

### Workflow Features

- **Target Framework**: Builds for `net8.0`
- **Testing**: Runs all unit and integration tests before packaging
- **Debug symbols**: Includes `.snupkg` files for debugging
- **Artifact upload**: Non-tagged builds create downloadable artifacts
- **Independent deployment**: Only deploys when Correspondence package changes

## License

Same as parent repository.