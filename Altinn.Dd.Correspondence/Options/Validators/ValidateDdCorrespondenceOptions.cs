using Altinn.Dd.Correspondence.Options;
using Microsoft.Extensions.Options;

namespace Altinn.Dd.Correspondence.Options.Validators;

internal partial class ValidateDdCorrespondenceOptions : IValidateOptions<DdCorrespondenceOptions>
{
    public ValidateOptionsResult Validate(string? name, DdCorrespondenceOptions options)
    {
        var resourceId = options.ResourceId;
        if (string.IsNullOrEmpty(resourceId))
        {
            return ValidateOptionsResult.Fail($"{nameof(resourceId)} must be provided.");
        }

        var maskinportenSettings = options.MaskinportenSettings;
        if (maskinportenSettings is null)
        {
            return ValidateOptionsResult.Fail($"{nameof(maskinportenSettings)} must be provided.");
        }
        if (maskinportenSettings.ClientId is null)
        {
            return ValidateOptionsResult.Fail($"{nameof(maskinportenSettings.ClientId)} must be provided.");
        }
        if (maskinportenSettings.Environment is null)
        {
            return ValidateOptionsResult.Fail($"{nameof(maskinportenSettings.Environment)} must be provided.");
        }

        return ValidateOptionsResult.Success;
    }
}
