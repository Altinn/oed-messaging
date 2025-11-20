using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Tests.Builders;

/// <summary>
/// Builder pattern for creating test instances of Settings
/// </summary>
public class SettingsBuilder
{
    private Settings _settings = new();

    public static SettingsBuilder Create() => new();

    public SettingsBuilder WithCorrespondenceSettings(string correspondenceSettings)
    {
        _settings.CorrespondenceSettings = correspondenceSettings;
        return this;
    }

    public SettingsBuilder WithBaseUrl(string baseUrl)
    {
        _settings.BaseUrl = baseUrl;
        return this;
    }

    public SettingsBuilder WithIgnoreReservation(bool ignoreReservation)
    {
        _settings.IgnoreReservation = ignoreReservation;
        return this;
    }

    /// <summary>
    /// Creates settings with valid defaults for testing.
    /// </summary>
    /// <param name="correspondenceSettings">Optional correspondence settings in format "resourceId,senderOrg". Defaults to test values if not provided.</param>
    /// <param name="baseUrl">Optional base URL. Defaults to test environment URL if not provided.</param>
    public SettingsBuilder WithValidDefaults(
        string? correspondenceSettings = null,
        string? baseUrl = null)
    {
        return WithCorrespondenceSettings(correspondenceSettings ?? "test-resource-id,test-sender-org")
               .WithBaseUrl(baseUrl ?? "https://platform.tt02.altinn.no");
    }

    public Settings Build() => _settings;
}
