using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Tests.Builders;

/// <summary>
/// Builder pattern for creating test instances of CorrespondenceSettings
/// </summary>
public class CorrespondenceSettingsBuilder
{
    private CorrespondenceSettings _settings = new()
    {
        Maskinporten = new MaskinportenSettings()
    };

    public static CorrespondenceSettingsBuilder Create() => new();

    public CorrespondenceSettingsBuilder WithResourceSettings(string resourceSettings)
    {
        _settings.ResourceSettings = resourceSettings;
        return this;
    }

    public CorrespondenceSettingsBuilder WithBaseUri(string baseUri)
    {
        _settings.BaseUri = baseUri;
        return this;
    }

    public CorrespondenceSettingsBuilder WithCountryCode(string countryCode)
    {
        _settings.CountryCode = countryCode;
        return this;
    }

    public CorrespondenceSettingsBuilder WithIgnoreReservation(bool ignoreReservation)
    {
        _settings.IgnoreReservation = ignoreReservation;
        return this;
    }

    public CorrespondenceSettingsBuilder WithMaskinporten(Action<MaskinportenSettings> configure)
    {
        configure(_settings.Maskinporten);
        return this;
    }

    /// <summary>
    /// Creates settings with valid defaults for testing.
    /// </summary>
    /// <param name="resourceSettings">Optional resource settings in format "resourceId,senderOrg". Defaults to test values if not provided.</param>
    /// <param name="baseUri">Optional base URI. Defaults to test environment URL if not provided.</param>
    public CorrespondenceSettingsBuilder WithValidDefaults(
        string? resourceSettings = null,
        string? baseUri = null)
    {
        return WithResourceSettings(resourceSettings ?? "test-resource-id,test-sender-org")
               .WithBaseUri(baseUri ?? "https://platform.tt02.altinn.no")
               .WithCountryCode("0192")
               .WithIgnoreReservation(true)
               .WithMaskinporten(m =>
               {
                   m.ClientId = "test-client-id";
                   m.Environment = "test";
                   m.EncodedJwk = "test-encoded-jwk";
               });
    }

    public CorrespondenceSettings Build() => _settings;
}
