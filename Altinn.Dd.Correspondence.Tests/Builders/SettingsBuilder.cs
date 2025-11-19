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

    public SettingsBuilder WithUseAltinnTestServers(bool useTestServers)
    {
        _settings.UseAltinnTestServers = useTestServers;
        return this;
    }

    public SettingsBuilder WithIgnoreReservation(bool ignoreReservation)
    {
        _settings.IgnoreReservation = ignoreReservation;
        return this;
    }

    public SettingsBuilder WithValidDefaults()
    {
        return WithCorrespondenceSettings("test-resource-id,test-sender-org")
               .WithUseAltinnTestServers(true);
    }

    public SettingsBuilder WithProductionSettings()
    {
        return WithCorrespondenceSettings("prod-resource-id,prod-sender-org")
               .WithUseAltinnTestServers(false);
    }

    public Settings Build() => _settings;
}
