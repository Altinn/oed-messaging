# Altinn.Dd.Correspondence

A .NET library for sending correspondence through Altinn 3 Correspondence API. This library follows Altinn 3 patterns for HttpClient registration with Maskinporten authentication.

## Quick Start

### 1. Install Packages

```bash
dotnet add package Altinn.Dd.Correspondence
dotnet add package Altinn.ApiClients.Maskinporten
```

**Note**: `Altinn.ApiClients.Maskinporten` is a peer dependency and must be installed separately.

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
    "CorrespondenceSettings": {
      "CorrespondenceSettings": "your-resource-id,your-sender-org",
      "UseAltinnTestServers": true,
      "CountryCode": "0192"
    }
  }
}
```

**Configuration Details**:
- `ClientId`: Your Maskinporten client ID (required)
- `Environment`: Either "test" or "prod" for Maskinporten environment
- `EncodedJwk`: Base64-encoded JWK from Azure Key Vault
- `EnableDebugLogging`: Optional flag to emit verbose Maskinporten diagnostics (only enable temporarily)
- `CorrespondenceSettings`: Format is "resourceId,senderOrg"
- `UseAltinnTestServers`: Set to `true` for TT02 test environment, `false` for production
- `CountryCode`: Country code for organization numbers (default: "0192" for Norway)

**Note**: The scope `"altinn:serviceowner altinn:correspondence.write"` is hardcoded in the library - you don't need to specify it.

### 3. Register Service

In your `Program.cs` or `Startup.cs`:

```csharp
using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// In ConfigureServices or builder.Services
services.AddDdMessagingService<SettingsJwkClientDefinition>(
    configuration.GetSection("DdConfig:MaskinportenSettings"),
    configuration.GetSection("DdConfig:CorrespondenceSettings"));
```

> `AddDdMessagingService` enforces the required correspondence scope (`altinn:serviceowner altinn:correspondence.write`) and wires up a Maskinporten-enabled `HttpClient` with Polly-based retries. Consumers only need to supply environment-specific credentials. Set `EnableDebugLogging` in configuration when troubleshooting.

### 4. Use the Service

Inject `IDdMessagingService` into your classes:

```csharp
public class MyService
{
    private readonly IDdMessagingService _messagingService;

    public MyService(IDdMessagingService messagingService)
    {
        _messagingService = messagingService;
    }

    public async Task SendCorrespondenceAsync()
    {
        var messageDetails = new DdMessageDetails
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

        var receipt = await _messagingService.SendMessage(messageDetails);
    }
}
```

## Features

**What the library does for you:**
- Automatic Maskinporten authentication via `Altinn.ApiClients.Maskinporten`
- Hardcoded scope for correspondence API (no need to configure)
- Resilient HTTP client with Polly retry policy (exponential backoff)
- Automatic organization number formatting
- Comprehensive logging and error handling
- Idempotency support to prevent duplicate messages

## Complete Example

See the `SendDdCorrespondence` project in this repository for a complete working example.

## API Reference

### DdMessageDetails

```csharp
public class DdMessageDetails
{
    public string? Recipient { get; set; }          // Organization number (9 digits)
    public string? Title { get; set; }              // Message title
    public string? Summary { get; set; }            // Brief summary (supports markdown)
    public string? Body { get; set; }               // Full message body (supports markdown)
    public string? Sender { get; set; }             // Sender name
    public DateTime? VisibleDateTime { get; set; }  // When message becomes visible
    public DateTime? ShipmentDatetime { get; set; } // When to send notifications
    public bool AllowForwarding { get; set; }       // Allow recipient to forward
    public Guid? IdempotencyKey { get; set; }       // Optional: auto-generated if null
    public NotificationDetails? Notification { get; set; } // Email/SMS notifications
}
```

### NotificationDetails

```csharp
public class NotificationDetails
{
    public string? EmailSubject { get; set; }  // Email notification subject
    public string? EmailBody { get; set; }     // Email notification body
    public string? SmsText { get; set; }       // SMS notification text
}
```

## Error Handling

```csharp
try
{
    var receipt = await messagingService.SendMessage(messageDetails);
}
catch (CorrespondenceServiceException ex)
{
    // Handle API errors
    _logger.LogError(ex, "Failed to send correspondence");
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

## Migration from Old Pattern

If you're migrating from the old `AddDdCorrespondence` pattern with `IAccessTokenProvider`:

**Before:**
```csharp
builder.Host.AddDdCorrespondence(settings, accessTokenProvider);
```

**After:**
```csharp
services.AddDdMessagingService<SettingsJwkClientDefinition>(
    configuration.GetSection("DdConfig:MaskinportenSettings"),
    configuration.GetSection("DdConfig:CorrespondenceSettings"));
```

The new pattern:
- Eliminates the need for custom `IAccessTokenProvider` implementations
- Uses the standard Altinn 3 Maskinporten HttpClient pattern
- Simplifies configuration (only ClientId, Environment, EncodedJwk, and optional `EnableDebugLogging` are required from appsettings)
- Hardcodes the scope internally (one less thing to configure)
- **Validation Errors**: Settings are validated using data annotations
- **API Errors**: Altinn 3 API errors are preserved and wrapped for consistency

## Example Implementation

### SendDdCorrespondence Project

This repository includes a working example project called `SendDdCorrespondence` that demonstrates:

- Complete implementation of `IAccessTokenProvider` using Maskinporten
- Proper service registration using `AddDdCorrespondence()` extension method
- Full correspondence sending workflow
- Configuration setup for both test and production environments

**Key features of the example:**
- Uses `Altinn.ApiClients.Maskinporten` for authentication
- Implements `MaskinportenTokenAdapter` as a reference implementation
- Includes proper error handling and logging
- Ready-to-run console application for testing

**To use the example:**
1. Navigate to the `SendDdCorrespondence` project
2. Configure your `appsettings.json` with your Maskinporten settings
3. Run `dotnet run` to test correspondence sending

This example serves as both a testing tool and a reference implementation for integrating the `Altinn.Dd.Correspondence` package into your applications.

## Migration from Altinn.Oed.Messaging

### Overview
This guide covers migrating from `Altinn.Oed.Messaging` to `Altinn.Dd.Correspondence` (Altinn 3). The new package keeps the same high-level service contract while simplifying the receipt model.

### Prerequisites
- `Altinn.Dd.Correspondence` v1.0.0 (supports net8.0)
- .NET 8.0+
- Maskinporten integration configured in your app

### Step 1: Update Package References
```xml
<!-- Remove old -->
<!-- <PackageReference Include="Altinn.Oed.Messaging" Version="0.10.2" /> -->

<!-- Add new -->
<PackageReference Include="Altinn.Dd.Correspondence" Version="1.0.0" />
```

### Step 2: Add Required Using Statements
```csharp
using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.ExternalServices.Correspondence;
using Altinn.Dd.Correspondence.Authentication;
```

### Step 3: Update Service Registration

**Remove old registration:**
```csharp
// OLD - Remove this
services.AddSingleton<IOedMessagingService, OedMessagingService>();
services.AddTransient<BearerTokenHandler>();
services.AddHttpClient<IOedMessagingService, OedMessagingService>()
    .AddHttpMessageHandler<BearerTokenHandler>();
```

**Replace with new registration:**
```csharp
// NEW - Add this
var settings = builder.Configuration.GetSection("AltinnMessagingSettings").Get<Settings>()!;

// Register your token provider (implement IAccessTokenProvider)
builder.Services.AddSingleton<IAccessTokenProvider>(sp =>
{
    var maskinportenService = sp.GetRequiredService<IMaskinportenService>();
    var logger = sp.GetRequiredService<ILogger<MaskinportenTokenAdapter>>();
    return new MaskinportenTokenAdapter(maskinportenService, builder.Configuration, logger);
});

// Register correspondence services
builder.Host.AddDdCorrespondence(settings, builder.Services.BuildServiceProvider().GetRequiredService<IAccessTokenProvider>());
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
  "AltinnMessagingSettings": {
    "CorrespondenceSettings": "your-resource-id,your-sender",
    "UseAltinnTestServers": true,
    "CountryCode": "0192"
  },
  "DdConfig": {
    "MaskinportenSettings": {
      "ClientId": "your-client-id",
      "Scope": "altinn:serviceowner/correspondence",
      "WellKnownEndpoint": "https://maskinporten.no/.well-known/oauth-authorization-server"
    }
  }
}
```

### Step 5: Update Namespace References

Update all files in your project that reference the old messaging package:

**Replace old namespaces:**
```csharp
// OLD
using Altinn.Oed.Messaging.Models;
using Altinn.Oed.Messaging.Services.Interfaces;
using Altinn.Oed.Messaging.ExternalServices.Correspondence;

// NEW
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.ExternalServices.Correspondence;
```

**Common files to update:**
- Service classes that use `IDdMessagingService`
- Models that use `DdMessageDetails` or `ReceiptExternal`
- Controllers or API endpoints that send correspondence
- Test files that mock or test messaging functionality
- Extension methods that work with receipts

### Step 6: Update ReceiptExternal Usage

The new `ReceiptExternal` has a simplified structure. Update all code that uses the old properties:

**Old ReceiptExternal Structure:**
```csharp
public class ReceiptExternal
{
    public ReceiptStatusEnum ReceiptStatusCode { get; set; }
    public string ReceiptText { get; set; }
    public ReferenceList References { get; set; }  // REMOVED
    public List<ReceiptExternal> SubReceipts { get; set; }  // REMOVED
}
```

**New ReceiptExternal Structure:**
```csharp
public class ReceiptExternal
{
    public ReceiptStatusEnum ReceiptStatusCode { get; set; }
    public string ReceiptText { get; set; }
}
```

**Update ReceiptExternalExtensions.cs:**
```csharp
// OLD - Remove this logic
if (receipt.References.IsNullOrEmpty())
{
    logger.LogCritical("Message receipt references is null or empty.");
    throw new Exception($"Message receipt references is null or empty.");
}

logger.LogInformation("Message sent successfully with {ReceiptReference}",
    receipt.References.FirstOrDefault()?.ReferenceValue);

// NEW - Replace with simplified logic
if (receipt is not { ReceiptStatusCode: ReceiptStatusEnum.OK })
{
    logger.LogError("Unable to send message. ReceiptStatusCode: {ReceiptStatusCode}, Message: {ReceiptText}", 
        receipt.ReceiptStatusCode.ToString(), receipt.ReceiptText);
    throw new Exception($"Unable to send message. ReceiptStatusCode: {receipt.ReceiptStatusCode.ToString()}, Message: {receipt.ReceiptText}");
}

logger.LogInformation("Message sent successfully. Message: {ReceiptText}",
    receipt.ReceiptText);
```

**Update CorrespondenceReceiptValidator.cs:**
```csharp
// OLD - Remove this logic
if (receipt.References.IsNullOrEmpty())
{
    logger.LogCritical("Message receipt references is null or empty.");
    throw new Exception($"Message receipt references is null or empty.");
}

logger.LogInformation("Message sent successfully with {ReceiptReference}",
    receipt.References.FirstOrDefault()?.ReferenceValue);

// NEW - Replace with simplified logic
if (receipt is not { ReceiptStatusCode: ReceiptStatusEnum.OK })
{
    logger.LogError("Unable to send message. ReceiptStatusCode: {ReceiptStatusCode}, Message: {ReceiptText}", 
        receipt.ReceiptStatusCode.ToString(), receipt.ReceiptText);
    throw new Exception($"Unable to send message. ReceiptStatusCode: {receipt.ReceiptStatusCode.ToString()}, Message: {receipt.ReceiptText}");
}

logger.LogInformation("Message sent successfully. Message: {ReceiptText}",
    receipt.ReceiptText);
```

### Step 7: Update Test Files

**Update Test Utilities:**
```csharp
// OLD
using Altinn.Oed.Messaging.Models;

// NEW
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.ExternalServices.Correspondence;
```

**Update GetReceipt Method:**
```csharp
// OLD
public static ReceiptExternal GetReceipt()
    => new()
    {
        References = new ReferenceList
        {
            new Reference()
        },
        ReceiptStatusCode = ReceiptStatusEnum.OK
    };

// NEW
public static ReceiptExternal GetReceipt()
    => new()
    {
        ReceiptStatusCode = ReceiptStatusEnum.OK,
        ReceiptText = "Message sent successfully"
    };
```

**Update Test Helpers:**
```csharp
// OLD
itemService.SendMessage(Arg.Any<DdMessageDetails>(), Arg.Any<string>())
    .Returns(new ReceiptExternal
    {
        References = new ReferenceList
        {
            new Reference()
        },
        ReceiptStatusCode = ReceiptStatusEnum.OK,
    });

// NEW
itemService.SendMessage(Arg.Any<DdMessageDetails>(), Arg.Any<string>())
    .Returns(new ReceiptExternal
    {
        ReceiptStatusCode = ReceiptStatusEnum.OK,
        ReceiptText = "Message sent successfully"
    });
```

**Update Test Files:**
Update all test files that reference the old messaging package:

```csharp
// OLD
using Altinn.Oed.Messaging.Models;
using Altinn.Oed.Messaging.Services.Interfaces;

// NEW
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.ExternalServices.Correspondence;
```

**Update test mocks and helpers:**
```csharp
// OLD
itemService.SendMessage(Arg.Any<DdMessageDetails>(), Arg.Any<string>())
    .Returns(new ReceiptExternal
    {
        References = new ReferenceList
        {
            new Reference()
        },
        ReceiptStatusCode = ReceiptStatusEnum.OK,
    });

// NEW
itemService.SendMessage(Arg.Any<DdMessageDetails>(), Arg.Any<string>())
    .Returns(new ReceiptExternal
    {
        ReceiptStatusCode = ReceiptStatusEnum.OK,
        ReceiptText = "Message sent successfully"
    });
```

**Fix ProblemDetails Ambiguity:**
If you have tests using `ProblemDetails`, you may need to fully qualify the type:

```csharp
// OLD
Assert.IsType<ProblemDetails>(objectActual.Value);
var problemDetailsActual = (ProblemDetails)objectActual.Value;

// NEW
Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(objectActual.Value);
var problemDetailsActual = (Microsoft.AspNetCore.Mvc.ProblemDetails)objectActual.Value;
```

## Troubleshooting

### Common Issues

1. **"Type not found"** (e.g., `ReceiptExternal`): add `using Altinn.Dd.Correspondence.ExternalServices.Correspondence;`
2. **"BearerTokenHandler not found"**: you don't register it manually; ensure `builder.Host.AddOedCorrespondence(...)` is called and Maskinporten adapter is registered as `IAccessTokenProvider`.
3. **NU1605/NU1202**: ensure your consuming app uses net8.0+ and that the package you're consuming is the net8.0 build (v1.0.0) with Microsoft.Extensions 8.0.x alignment.
4. **"IAccessTokenProvider not found"**: implement the interface or use the provided `MaskinportenTokenAdapter` example.

## API Reference

### Core Interfaces

- `IDdMessagingService`: Main service for sending correspondence
- `IAccessTokenProvider`: Authentication token provider

### Key Models

- `DdMessageDetails`: Complete correspondence information (includes optional `IdempotencyKey` property)
- `NotificationDetails`: Optional email/SMS notification settings
- `Settings`: Service configuration
- `ReceiptExternal`: Response from correspondence service

### Extension Methods

- `AddDdCorrespondence()`: Registers all correspondence services with HttpClient, authentication, and retry policies

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