# Altinn.Oed.Correspondence

A .NET library for sending correspondence through Altinn 3 Correspondence API.

## Quick Start

### 1. Install Package

```bash
dotnet add package Altinn.Oed.Correspondence
```

### 2. Configure Settings

```csharp
var settings = new Settings
{
    CorrespondenceSettings = "your-resource-id,your-sender-org",
    UseAltinnTestServers = true // Use TT02 for testing
};
```

### 3. Implement Token Provider

```csharp
public class YourTokenProvider : IAccessTokenProvider
{
    public async Task<string> GetAccessTokenAsync()
    {
        // Return your OAuth2/JWT Bearer token
        return "your-access-token";
    }
}
```

### 4. Register Services

```csharp
var tokenProvider = new YourTokenProvider();
hostBuilder.AddOedCorrespondence(settings, tokenProvider);
```

### 5. Send Correspondence

```csharp
var messagingService = serviceProvider.GetRequiredService<IOedMessagingService>();

var correspondence = new OedMessageDetails
{
    Recipient = "12345678901",
    Title = "Important Notice",
    Summary = "Brief summary",
    Body = "Full message content",
    Sender = "Your Organization",
    VisibleDateTime = DateTime.Now
};

var result = await messagingService.SendMessage(correspondence);
```

## Maskinporten Authentication

For production use with Maskinporten, see the reference implementation in `SendOedCorrespondence` project using `Altinn.ApiClients.Maskinporten` v9.2.1.

## Migration from Altinn.Oed.Messaging

### Overview
This guide covers migrating from `Altinn.Oed.Messaging` to `Altinn.Oed.Correspondence` (Altinn 3). The new package keeps the same high-level service contract while simplifying the receipt model.

### Prerequisites
- `Altinn.Oed.Correspondence` v1.0.0 (supports net8.0 and net9.0)
- .NET 8.0+ or .NET 9.0+
- Maskinporten integration configured in your app

### 1. Update Package References
```xml
<!-- Remove old -->
<!-- <PackageReference Include="Altinn.Oed.Messaging" Version="0.10.2" /> -->

<!-- Add new -->
<PackageReference Include="Altinn.Oed.Correspondence" Version="1.0.0" />
```

### 2. Required usings
```csharp
using Altinn.Oed.Correspondence.Extensions;
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Altinn.Oed.Correspondence.ExternalServices.Correspondence;
using Altinn.Oed.Correspondence.Authentication;
```

### 3. Service Registration
Use the provided host extension. Do not manually register the HttpClient or handlers for this service.

```csharp
var settings = builder.Configuration.GetSection("AltinnMessagingSettings").Get<Settings>()!;
services.AddSingleton<IOedNotificationSettings>(settings);

// Provide IAccessTokenProvider via your Maskinporten adapter
services.AddSingleton<IAccessTokenProvider>(sp =>
{
    var maskinporten = sp.GetRequiredService<IMaskinportenService>();
    var logger = sp.GetRequiredService<ILogger<MaskinportenTokenAdapter>>();
    return new MaskinportenTokenAdapter(
        maskinporten,
        builder.Configuration.GetSection("OedConfig:MaskinportenSettings"),
        logger);
});

// Wire the correspondence client via the host extension
builder.Host.AddOedCorrespondence(
    settings,
    services.BuildServiceProvider().GetRequiredService<IAccessTokenProvider>());
```

Remove any old code like:
```csharp
// OLD – remove
services.AddSingleton<IOedMessagingService, OedMessagingService>();
services.AddTransient<BearerTokenHandler>();
services.AddHttpClient<IOedMessagingService, OedMessagingService>()
    .AddHttpMessageHandler<BearerTokenHandler>();
```

### 4. Configuration
The new `Settings` expects these keys:

```json
{
  "AltinnMessagingSettings": {
    "CorrespondenceSettings": "your-resource-id,your-sender",
    "UseAltinnTestServers": true,
    "CountryCode": "0192"
  },
  "OedConfig": {
    "MaskinportenSettings": {
      "ClientId": "your-client-id",
      "Scope": "altinn:serviceowner/correspondence",
      "WellKnownEndpoint": "https://maskinporten.no/.well-known/oauth-authorization-server"
    }
  }
}
```

Notes:
- `CorrespondenceSettings` is a single string: "resourceId,sender"
- `CountryCode` must be 3–4 chars; default is "0192"

### 5. Namespace Updates
Change old imports to:
```csharp
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Altinn.Oed.Correspondence.ExternalServices.Correspondence; // for ReceiptExternal
```

### 6. ReceiptExternal Simplification
Old properties removed: `References`, `SubReceipts`. Keep:
- `ReceiptStatusCode` (enum: `OK`, `Error`)
- `ReceiptText`

Update example:
```csharp
if (receipt is not { ReceiptStatusCode: ReceiptStatusEnum.OK })
{
    logger.LogError("Unable to send message. Status: {Status}, Message: {Text}",
        receipt.ReceiptStatusCode.ToString(), receipt.ReiptText);
    throw new Exception($"Unable to send message. Status: {receipt.ReceiptStatusCode}, Message: {receipt.ReceiptText}");
}

logger.LogInformation("Message sent successfully. Message: {Text}", receipt.ReceiptText);
```

### 7. Service Usage
```csharp
var messaging = app.Services.GetRequiredService<IOedMessagingService>();

var message = new OedMessageDetails
{
    Recipient = "12345678901",
    Title = "Message Title",
    Summary = "Short summary",
    Body = "Main content",
    Sender = "Optional sender",
    VisibleDateTime = DateTime.UtcNow,
    ShipmentDatetime = DateTime.UtcNow.AddMinutes(1),
    AllowForwarding = false,
    Notification = new NotificationDetails
    {
        SmsText = "SMS text",
        EmailSubject = "Email subject",
        EmailBody = "Email body"
    }
};

var receipt = await messaging.SendMessage(message);
if (receipt.ReceiptStatusCode != ReceiptStatusEnum.OK)
{
    throw new Exception($"Altinn 3 send failed: {receipt.ReceiptText}");
}
```

### Troubleshooting
- **"Type not found"** (e.g., `ReceiptExternal`): add `using Altinn.Oed.Correspondence.ExternalServices.Correspondence;`
- **"BearerTokenHandler not found"**: you don't register it manually; ensure `builder.Host.AddOedCorrespondence(...)` is called and Maskinporten adapter is registered as `IAccessTokenProvider`.
- **NU1605/NU1202**: ensure your consuming app uses net8.0 and that the package you're consuming is the net8.0 build (v1.0.2) with Microsoft.Extensions 8.0.x alignment.

### Benefits
- Direct Altinn 3 integration
- Simpler receipt model
- Built-in resilient HttpClient wiring (Polly) via the host extension
- Maintains the `IOedMessagingService` contract for minimal code changes

## API Reference

### Core Interfaces

- `IOedMessagingService`: Main service for sending correspondence
- `IAccessTokenProvider`: Authentication token provider

### Key Models

- `OedMessageDetails`: Complete correspondence information
- `NotificationDetails`: Optional email/SMS notification settings
- `Settings`: Service configuration

## License

Same as parent repository.