using Altinn.ApiClients.Maskinporten.Config;

namespace Altinn.Dd.Correspondence.Options;

/// <summary>
/// Configuration for the correspondence client, bound from the configuration section passed to
/// <see cref="Extensions.ServiceCollectionExtensions.AddDdCorrespondenceService(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, System.Action{DdCorrespondenceOptions})"/>.
/// </summary>
public class DdCorrespondenceOptions
{
    /// <summary>
    /// Gets or sets the Maskinporten client credentials. The required correspondence scopes are
    /// applied by the library, so only the client id, environment and key need to be supplied.
    /// </summary>
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
