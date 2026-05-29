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
    /// Gets or sets the Authentik group name that grants basic Jellyfin access.
    /// </summary>
    public string AllowedGroup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether users should be auto-created on first login.
    /// </summary>
    public bool AutoCreateUsers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Authentik groups should sync to Jellyfin permissions.
    /// </summary>
    public bool EnableGroupSync { get; set; }
}
