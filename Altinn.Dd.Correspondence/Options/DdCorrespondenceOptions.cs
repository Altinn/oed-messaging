using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.Dd.Correspondence.Options;

public class DdCorrespondenceOptions
{
    public required MaskinportenSettings MaskinportenSettings { get; set; }

    /// <summary>
    /// Gets or sets the correspondence resource ID.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the environment to use for API requests (e.g., Development for local, Staging for TT02, Production for live systems).
    /// </summary>
    public ApiEnvironment Environment { get; set; } = ApiEnvironment.Development;
}
