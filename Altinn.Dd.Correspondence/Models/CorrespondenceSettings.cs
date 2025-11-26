using Altinn.ApiClients.Maskinporten.Config;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// Settings for Altinn DD Correspondence client, following Dialogporten pattern
/// </summary>
public sealed class CorrespondenceSettings
{
    /// <summary>
    /// Gets or sets the base URI for the Altinn platform API (e.g., "https://platform.altinn.no" or "https://platform.tt02.altinn.no")
    /// </summary>
    [Required(ErrorMessage = "BaseUri is required")]
    [Url(ErrorMessage = "BaseUri must be a valid URL")]
    public string BaseUri { get; set; } = null!;

    /// <summary>
    /// Gets or sets the resource settings in format "resourceId,sender"
    /// </summary>
    [Required(ErrorMessage = "ResourceSettings is required")]
    [RegularExpression(@"^[^,]+,[^,]+$", ErrorMessage = "ResourceSettings must be in format 'resourceId,sender'")]
    public string ResourceSettings { get; set; } = null!;

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

    /// <summary>
    /// Gets or sets the Maskinporten settings for authentication
    /// </summary>
    [Required(ErrorMessage = "Maskinporten settings are required")]
    public MaskinportenSettings Maskinporten { get; set; } = null!;

    /// <summary>
    /// Validates the settings configuration
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public static bool Validate(CorrespondenceSettings? settings)
    {
        if (settings == null) return false;

        var validationContext = new ValidationContext(settings);
        var validationResults = new List<ValidationResult>();
        return Validator.TryValidateObject(settings, validationContext, validationResults, true);
    }
}
