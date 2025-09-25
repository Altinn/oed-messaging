using Altinn.Oed.Correspondence.Models.Interfaces;

namespace Altinn.Oed.Correspondence.Models;

/// <summary>
/// Settings class for Altinn 3 Correspondence service configuration
/// </summary>
public class Settings : IOedNotificationSettings
{
    /// <summary>
    /// Gets or sets the correspondence settings in format "resourceId,sender"
    /// </summary>
    public string CorrespondenceSettings { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether to use Altinn test servers (TT02) instead of production
    /// </summary>
    public bool UseAltinnTestServers { get; set; } = false;
}
