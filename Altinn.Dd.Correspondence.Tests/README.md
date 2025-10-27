# Altinn.Dd.Correspondence.Tests

This project contains comprehensive unit and integration tests for the Altinn.Dd.Correspondence library.

## Test Structure

### Unit Tests
- **DdMessagingServiceTests**: Tests the core messaging service functionality
- **ModelsTests**: Tests all model classes and their properties
- **CorrespondenceServiceExceptionTests**: Tests custom exception handling
- **ReceiptExternalTests**: Tests the receipt response classes

### Integration Tests
- **DdMessagingServiceIntegrationTests**: Tests dependency injection and service configuration

### Test Data Builders
- **DdMessageDetailsBuilder**: Builder pattern for creating test message details
- **NotificationDetailsBuilder**: Builder pattern for creating test notification details
- **SettingsBuilder**: Builder pattern for creating test settings

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "ClassName=DdMessagingServiceTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Coverage

The tests cover:
- ✅ Service initialization and configuration
- ✅ Message sending with various notification types
- ✅ Error handling and exception scenarios
- ✅ Model validation and serialization
- ✅ Dependency injection setup
- ✅ Backward compatibility with Altinn 2 interface

## Test Dependencies

- **xUnit**: Testing framework
- **FluentAssertions**: Fluent assertion library
- **Moq**: Mocking framework for HTTP client testing
- **Microsoft.Extensions.Http**: HTTP client testing support

## Notes

- Tests use mocked HTTP clients to avoid real API calls during unit testing
- Integration tests verify actual service registration and configuration
- Some tests expect authentication failures when using placeholder credentials (this is expected behavior)
