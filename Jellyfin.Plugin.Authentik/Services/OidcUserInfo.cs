using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Authentik.Services;

/// <summary>
/// Represents the user info returned from Authentik's userinfo endpoint.
/// </summary>
public class OidcUserInfo
{
    /// <summary>
    /// Gets or sets the subject (unique user ID in Authentik).
    /// </summary>
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preferred username.
    /// </summary>
    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the groups the user belongs to.
    /// </summary>
    [JsonPropertyName("groups")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs setter for System.Text.Json deserialization")]
    public Collection<string> Groups { get; set; } = new();
}
