using Altinn.Dd.Correspondence.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// Settings class for Altinn 3 Correspondence service configuration
/// </summary>
public class Settings : IDdNotificationSettings
{
    /// <summary>
    /// Gets or sets the correspondence settings in format "resourceId,sender"
    /// </summary>
    [Required(ErrorMessage = "CorrespondenceSettings is required")]
    [RegularExpression(@"^[^,]+,[^,]+$", ErrorMessage = "CorrespondenceSettings must be in format 'resourceId,sender'")]
    public string CorrespondenceSettings { get; set; } = null!;

    /// <summary>
    /// Gets or sets the base URL for the Altinn platform API (e.g., "https://platform.altinn.no" or "https://platform.tt02.altinn.no")
    /// </summary>
    [Required(ErrorMessage = "BaseUrl is required")]
    [Url(ErrorMessage = "BaseUrl must be a valid URL")]
    public string BaseUrl { get; set; } = null!;

    /// <summary>
    /// Gets or sets the country code for organization number formatting (default: "0192" for Norway)
    /// </summary>
    [Required(ErrorMessage = "CountryCode is required")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CountryCode must be between 3 and 4 characters")]
    public string CountryCode { get; set; } = "0192";

    /// <summary>
    /// Gets or sets whether Altinn should ignore reservation flags (KRR) when sending correspondences
    /// </summary>
    public bool IgnoreReservation { get; set; } = true;
}
