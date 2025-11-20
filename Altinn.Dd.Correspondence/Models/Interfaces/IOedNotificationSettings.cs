namespace Altinn.Dd.Correspondence.Models.Interfaces;

/// <summary>
/// Settings interface for Altinn 3 Correspondence service configuration
/// </summary>
public interface IDdNotificationSettings
{
    /// <summary>
    /// Gets or sets the correspondence settings in format "resourceId,sender"
    /// </summary>
    public string CorrespondenceSettings { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the Altinn platform API (e.g., "https://platform.altinn.no" or "https://platform.tt02.altinn.no")
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the country code for organization number formatting (default: "0192" for Norway)
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets whether Altinn should ignore reservation flag (KRR) when sending correspondences
    /// </summary>
    public bool IgnoreReservation { get; set; }
}
