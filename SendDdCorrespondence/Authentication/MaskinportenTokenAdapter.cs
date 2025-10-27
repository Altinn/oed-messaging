using Altinn.ApiClients.Maskinporten.Services;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Models;
using Altinn.Dd.Correspondence.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SendDdCorrespondence.Authentication;

/// <summary>
/// Adapter that bridges Altinn's Maskinporten service to our IAccessTokenProvider interface.
/// This allows the library to work with Altinn's official Maskinporten implementation.
/// </summary>
public class MaskinportenTokenAdapter : IAccessTokenProvider
{
    private readonly IMaskinportenService _maskinportenService;
    private readonly IConfigurationSection _maskinportenSettings;
    private readonly ILogger<MaskinportenTokenAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaskinportenTokenAdapter"/> class.
    /// </summary>
    /// <param name="maskinportenService">The Altinn Maskinporten service instance</param>
    /// <param name="maskinportenSettings">The Maskinporten configuration section</param>
    /// <param name="logger">Logger for debugging</param>
    public MaskinportenTokenAdapter(IMaskinportenService maskinportenService, IConfigurationSection maskinportenSettings, ILogger<MaskinportenTokenAdapter> logger)
    {
        _maskinportenService = maskinportenService ?? throw new ArgumentNullException(nameof(maskinportenService));
        _maskinportenSettings = maskinportenSettings ?? throw new ArgumentNullException(nameof(maskinportenSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var clientId = _maskinportenSettings["ClientId"];
            var scope = _maskinportenSettings["Scope"];
            var environment = _maskinportenSettings["Environment"];
            var encodedJwk = _maskinportenSettings["EncodedJwk"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(environment) || string.IsNullOrEmpty(encodedJwk))
            {
                throw new InvalidOperationException("Missing required Maskinporten configuration: ClientId, Scope, Environment, or EncodedJwk");
            }

            _logger.LogDebug("Requesting token from Maskinporten for client {ClientId} with scope {Scope} in environment {Environment}", clientId, scope, environment);

            // Use the base64 encoded JWK directly - this matches the OED project format
            var tokenResponse = await _maskinportenService.GetToken(encodedJwk, environment, clientId, scope, null, null, false);
            return tokenResponse.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain access token from Maskinporten service");
            throw new InvalidOperationException(
                "Failed to obtain access token from Maskinporten service. Check your configuration and ensure the JWK and client ID are correct.", 
                ex);
        }
    }

}