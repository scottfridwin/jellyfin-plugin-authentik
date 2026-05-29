using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Authentik.Services;

/// <summary>
/// Represents the OIDC token response.
/// </summary>
public class OidcTokenResponse
{
    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID token.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token type.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expiration in seconds.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
