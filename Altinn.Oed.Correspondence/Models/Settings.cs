using Altinn.Oed.Correspondence.Models.Interfaces;

namespace Altinn.Oed.Correspondence.Models;
public class Settings : IOedNotificationSettings
{
    public string AltinnServiceAddress { get; set; } = null!;
    public string CorrespondenceSettings { get; set; } = null!;
    public string AgencySystemUserName { get; set; } = null!;
    public string AgencySystemPassword { get; set; } = null!;
    public bool UseAltinnTestServers { get; set; } = false;
}
