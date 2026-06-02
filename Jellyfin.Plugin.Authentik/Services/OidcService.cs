using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Plugin.Authentik.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Authentik.Services;

/// <summary>
/// Handles OIDC authentication flows with Authentik.
/// </summary>
public class OidcService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OidcService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public OidcService(IHttpClientFactory httpClientFactory, ILogger<OidcService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Generates the authorization URL to redirect the user to Authentik.
    /// </summary>
    /// <param name="redirectUri">The callback URI after authentication.</param>
    /// <param name="state">The state parameter for CSRF protection.</param>
    /// <param name="codeVerifier">Output PKCE code verifier to store in session.</param>
    /// <returns>The authorization URL.</returns>
    public string GetAuthorizationUrl(string redirectUri, string state, out string codeVerifier)
    {
        var config = Plugin.Instance!.Configuration;
        codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid profile email groups",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };

        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
        return $"{config.AuthentikUrl.TrimEnd('/')}/application/o/authorize/?{queryString}";
    }

    /// <summary>
    /// Exchanges an authorization code for tokens.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirect URI used in the original request.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <returns>The token response containing user claims.</returns>
    public async Task<OidcTokenResponse?> ExchangeCodeAsync(string code, string redirectUri, string codeVerifier)
    {
        var config = Plugin.Instance!.Configuration;
        var client = _httpClientFactory.CreateClient();

        var tokenEndpoint = $"{config.AuthentikUrl.TrimEnd('/')}/application/o/token/";

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["code_verifier"] = codeVerifier,
        };

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(parameters)).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed: {Status}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OidcTokenResponse>().ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches user info from the userinfo endpoint using the access token.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <returns>The user info claims.</returns>
    public async Task<OidcUserInfo?> GetUserInfoAsync(string accessToken)
    {
        var config = Plugin.Instance!.Configuration;
        var client = _httpClientFactory.CreateClient();

        var userInfoEndpoint = $"{config.AuthentikUrl.TrimEnd('/')}/application/o/userinfo/";

        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("UserInfo request failed: {Status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var userInfo = System.Text.Json.JsonSerializer.Deserialize<OidcUserInfo>(json);

        if (userInfo is not null && !string.IsNullOrWhiteSpace(config.ProfileImageClaim))
        {
            userInfo.Picture = ExtractClaimValue(json, config.ProfileImageClaim);
        }

        return userInfo;
    }

    /// <summary>
    /// Extracts a value from a JSON string using a dot-notation path (e.g. "attributes.avatar").
    /// </summary>
    private static string? ExtractClaimValue(string json, string claimPath)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var element = doc.RootElement;

            foreach (var segment in claimPath.Split('.'))
            {
                if (element.ValueKind != System.Text.Json.JsonValueKind.Object ||
                    !element.TryGetProperty(segment, out element))
                {
                    return null;
                }
            }

            return element.ValueKind == System.Text.Json.JsonValueKind.String
                ? element.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
