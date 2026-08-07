using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Authentik.Configuration;

/// <summary>
/// Plugin configuration for Authentik SSO.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        AuthentikUrl = string.Empty;
        ClientId = string.Empty;
        ClientSecret = string.Empty;
        AdminGroup = "jellyfin-admins";
        AllowedGroup = "jellyfin-users";
        AutoCreateUsers = true;
        EnableGroupSync = true;
        EnableContentPolicySync = false;
        GGroup = string.Empty;
        TvY7Group = string.Empty;
        PgGroup = string.Empty;
        Pg13Group = string.Empty;
        Tv14Group = string.Empty;
        EnableProfileImageSync = true;
        ProfileImageClaim = "picture";
        ForceHttpsRedirect = false;
    }

    /// <summary>
    /// Gets or sets the Authentik instance URL (e.g. https://auth.example.com).
    /// </summary>
    public string AuthentikUrl { get; set; }

    /// <summary>
    /// Gets or sets the OIDC client ID.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OIDC client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name that grants Jellyfin admin privileges.
    /// </summary>
    public string AdminGroup { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name required to access Jellyfin.
    /// Leave empty to allow all authenticated Authentik users.
    /// </summary>
    public string AllowedGroup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force HTTPS in the redirect URI.
    /// Enable this if Jellyfin is behind a reverse proxy that terminates TLS, otherwise
    /// the callback URL will use http:// and cause a redirect_uri mismatch in Authentik.
    /// </summary>
    public bool ForceHttpsRedirect { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether users should be auto-created on first login.
    /// </summary>
    public bool AutoCreateUsers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Authentik groups should sync to Jellyfin permissions.
    /// </summary>
    public bool EnableGroupSync { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Authentik groups should sync to Jellyfin parental rating restrictions.
    /// </summary>
    public bool EnableContentPolicySync { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name that restricts users to G, TV-G, and TV-Y content.
    /// </summary>
    public string GGroup { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name that restricts users to TV-Y7 and below.
    /// </summary>
    public string TvY7Group { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name that restricts users to PG and TV-PG content.
    /// </summary>
    public string PgGroup { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name that restricts users to PG-13 and below.
    /// </summary>
    public string Pg13Group { get; set; }

    /// <summary>
    /// Gets or sets the Authentik group name that restricts users to TV-14 and below.
    /// </summary>
    public string Tv14Group { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to sync the user's profile image from Authentik.
    /// </summary>
    public bool EnableProfileImageSync { get; set; }

    /// <summary>
    /// Gets or sets the claim path used to extract the profile image URL from the userinfo response.
    /// Supports dot-notation for nested fields (e.g. "attributes.avatar"). Default: "attributes.avatar".
    /// </summary>
    public string ProfileImageClaim { get; set; }
}
