namespace Altinn.Oed.Messaging.Models.Interfaces;
public interface IOedNotificationSettings
{
    public string AltinnServiceAddress { get; set; }
    public string CorrespondenceSettings { get; set; }
    public string AgencySystemUserName { get; set; }
    public string AgencySystemPassword { get; set; }
    public bool UseAltinnTestServers { get; set; }
}
