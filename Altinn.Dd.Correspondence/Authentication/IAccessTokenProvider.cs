namespace Altinn.Dd.Correspondence.Authentication;

/// <summary>
/// Interface for providing access tokens for API authentication.
/// Implementations should handle token acquisition, caching, and refresh logic.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Retrieves a valid access token for authenticating API requests.
    /// </summary>
    /// <returns>A valid access token string</returns>
    Task<string> GetAccessTokenAsync();
}

