# Altinn.Oed.Correspondence

This library provides a wrapper around the Altinn 3 Correspondence API for messaging heirs in the digital estate (OED) system. It maintains compatibility with the existing Altinn 2 messaging interface to ensure seamless migration.

## Features

- **Altinn 3 API Integration**: Uses the modern Altinn 3 REST API instead of legacy WCF services
- **Backward Compatibility**: Maintains the same public interface as the Altinn 2 version
- **Modern HTTP Client**: Leverages .NET's HttpClient for improved performance and reliability
- **Comprehensive Error Handling**: Proper exception mapping and error handling
- **Flexible Configuration**: Supports both test and production Altinn environments

## Usage

### Basic Setup

```csharp
using Altinn.Oed.Correspondence.Extensions;
using Altinn.Oed.Correspondence.Models;

var settings = new Settings
{
    CorrespondenceSettings = "your-resource-id,your-sender-org", // Altinn 3 format
    UseAltinnTestServers = true // Use TT02 for testing
};

hostBuilder.AddOedCorrespondence(settings);
```

### Sending Correspondence

```csharp
var messagingService = serviceProvider.GetService<IOedMessagingService>();

var correspondence = new OedMessageDetails
{
    Recipient = "12345678901", // Organization number or SSN
    Title = "Important Notice",
    Summary = "Summary of the correspondence",
    Body = "Full body content of the correspondence",
    Sender = "Your Organization Name",
    VisibleDateTime = DateTime.Now,
    Notification = new NotificationDetails
    {
        EmailSubject = "Email Subject",
        EmailBody = "Email body content",
        SmsText = "SMS text content"
    }
};

var result = await messagingService.SendMessage(correspondence);
```

## Migration from Altinn 2

### Configuration Changes

**Altinn 2 Format:**
```csharp
CorrespondenceSettings = "serviceCode,serviceEdition,userCode"
AgencySystemUserName = "username"
AgencySystemPassword = "password"
AltinnServiceAddress = "https://service.altinn.no"
```

**Altinn 3 Format:**
```csharp
CorrespondenceSettings = "resourceId,sender" // Simplified format
UseAltinnTestServers = true // Boolean flag for environment
```

### Key Differences

1. **Authentication**: Altinn 3 uses modern OAuth2/JWT tokens instead of username/password
2. **API Endpoints**: REST API instead of WCF services
3. **Configuration**: Simplified configuration with fewer parameters
4. **Error Handling**: Modern exception types with better error information

### Exception Handling

The service throws `CorrespondenceServiceException` instead of `MessagingServiceException`:

```csharp
try
{
    var result = await messagingService.SendMessage(correspondence);
}
catch (CorrespondenceServiceException ex)
{
    // Handle correspondence-specific errors
    Console.WriteLine($"Correspondence failed: {ex.Message}");
}
```

## API Reference

### IOedMessagingService

The main service interface for sending correspondences.

#### Methods

- `Task<ReceiptExternal> SendMessage(OedMessageDetails correspondence)`: Creates and sends a correspondence

### Models

#### OedMessageDetails
Contains all information needed to create a correspondence:
- `Recipient`: Organization number or SSN
- `Title`: Correspondence title
- `Summary`: Brief summary
- `Body`: Full content
- `Sender`: Sender organization name
- `VisibleDateTime`: When to make visible
- `Notification`: Notification settings

#### NotificationDetails
Controls how recipients are notified:
- `EmailSubject`: Email subject line
- `EmailBody`: Email content
- `SmsText`: SMS content

#### Settings
Configuration for the service:
- `CorrespondenceSettings`: Resource ID and sender (comma-separated)
- `UseAltinnTestServers`: Use TT02 environment

## Dependencies

- .NET 6.0
- Microsoft.Extensions.Http
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Hosting

## License

This project is licensed under the same terms as the parent repository.
