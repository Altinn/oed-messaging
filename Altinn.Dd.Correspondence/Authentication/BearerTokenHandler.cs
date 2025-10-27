using System.Net.Http.Headers;

namespace Altinn.Dd.Correspondence.Authentication;

/// <summary>
/// HTTP message handler that adds Bearer token authentication to outgoing requests.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IAccessTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenHandler"/> class.
    /// </summary>
    /// <param name="tokenProvider">The token provider to use for obtaining access tokens</param>
    public BearerTokenHandler(IAccessTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get the access token from the provider
        var accessToken = await _tokenProvider.GetAccessTokenAsync();

        // Add the Bearer token to the Authorization header
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Continue with the request
        return await base.SendAsync(request, cancellationToken);
    }
}

