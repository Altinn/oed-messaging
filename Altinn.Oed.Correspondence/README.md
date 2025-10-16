# Altinn.Oed.Correspondence

A .NET library for sending correspondence through Altinn 3 Correspondence API. Provides backward compatibility with Altinn 2 messaging interface.

## Features

- **Altinn 3 Integration**: Uses modern REST API instead of WCF services
- **Backward Compatible**: Same interface as Altinn 2 version
- **Flexible Authentication**: Works with any token provider implementation
- **Minimal Dependencies**: Only essential .NET packages

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

## Migration from Altinn 2

### Configuration Changes

**Before (Altinn 2):**
```csharp
CorrespondenceSettings = "serviceCode,serviceEdition,userCode"
AgencySystemUserName = "username"
AgencySystemPassword = "password"
```

**After (Altinn 3):**
```csharp
CorrespondenceSettings = "resourceId,sender"
// No username/password - use IAccessTokenProvider instead
```

### Key Differences

1. **Authentication**: OAuth2/JWT Bearer tokens instead of username/password
2. **API**: REST instead of WCF services
3. **Dependencies**: Must provide `IAccessTokenProvider` implementation

## API Reference

### Core Interfaces

- `IOedMessagingService`: Main service for sending correspondence
- `IAccessTokenProvider`: Authentication token provider

### Key Models

- `OedMessageDetails`: Complete correspondence information
- `NotificationDetails`: Optional email/SMS notification settings
- `Settings`: Service configuration

## Dependencies

- .NET 6.0+
- Microsoft.Extensions.Http
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Hosting

## License

Same as parent repository.