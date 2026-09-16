using RichardSzalay.MockHttp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Altinn.Dd.Correspondence.Tests;

/// <summary>
/// Stubs out the Maskinporten token exchange, so a test can drive the real DI pipeline without
/// credentials or network access. Registering these first leaves the correspondence endpoints for
/// the test itself to set up.
/// </summary>
internal static class MaskinportenStub
{
    /// <summary>Answers the metadata, token and Altinn token-exchange calls with dummy values.</summary>
    public static MockHttpMessageHandler StubTokenExchange(this MockHttpMessageHandler mockHttp)
    {
        mockHttp.When(HttpMethod.Get, "*/.well-known/oauth-authorization-server")
                .Respond("application/json", JsonSerializer.Serialize(new
                {
                    issuer = "https://test.maskinporten.no/",
                    token_endpoint = "https://test.maskinporten.no/token"
                }));

        mockHttp.When(HttpMethod.Post, "*/token")
                .Respond("application/json", JsonSerializer.Serialize(new
                {
                    access_token = "dummy-test-token",
                    token_type = "Bearer",
                    expires_in = 3599,
                    scope = "altinn:serviceowner/correspondence.write"
                }));

        mockHttp.When(HttpMethod.Get, "*/authentication/api/v1/exchange/maskinporten")
                .Respond("application/json", JsonSerializer.Serialize("dummy-altinn-token"));

        return mockHttp;
    }

    /// <summary>
    /// Generates a throwaway RSA JWK, so the tests need no real Maskinporten key. The key is never
    /// used to sign anything that is verified - the token endpoint above accepts whatever it gets.
    /// </summary>
    public static string GenerateEncodedTestJwk()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(true);

        var jwk = new
        {
            kty = "RSA",
            use = "sig",
            kid = "test-key",
            alg = "RS256",
            n = Base64Url(p.Modulus!),
            e = Base64Url(p.Exponent!),
            d = Base64Url(p.D!),
            p = Base64Url(p.P!),
            q = Base64Url(p.Q!),
            dp = Base64Url(p.DP!),
            dq = Base64Url(p.DQ!),
            qi = Base64Url(p.InverseQ!)
        };

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(jwk)));
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
