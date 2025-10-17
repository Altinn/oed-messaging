using Altinn.Oed.Correspondence.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Oed.Correspondence.Models;

/// <summary>
/// Settings class for Altinn 3 Correspondence service configuration
/// </summary>
public class Settings : IOedNotificationSettings
{
    /// <summary>
    /// Gets or sets the correspondence settings in format "resourceId,sender"
    /// </summary>
    [Required(ErrorMessage = "CorrespondenceSettings is required")]
    [RegularExpression(@"^[^,]+,[^,]+$", ErrorMessage = "CorrespondenceSettings must be in format 'resourceId,sender'")]
    public string CorrespondenceSettings { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether to use Altinn test servers (TT02) instead of production
    /// </summary>
    public bool UseAltinnTestServers { get; set; } = false;

    /// <summary>
    /// Gets or sets the country code for organization number formatting (default: "0192" for Norway)
    /// </summary>
    [Required(ErrorMessage = "CountryCode is required")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CountryCode must be between 3 and 4 characters")]
    public string CountryCode { get; set; } = "0192";
}
