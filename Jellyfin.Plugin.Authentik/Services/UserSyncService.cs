using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Authentik.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Authentik.Services;

/// <summary>
/// Handles user provisioning and permission sync from Authentik groups.
/// </summary>
public class UserSyncService
{
    private readonly IUserManager _userManager;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ILogger<UserSyncService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSyncService"/> class.
    /// </summary>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <param name="cryptoProvider">The crypto provider for password generation.</param>
    /// <param name="logger">The logger.</param>
    public UserSyncService(IUserManager userManager, ICryptoProvider cryptoProvider, ILogger<UserSyncService> logger)
    {
        _userManager = userManager;
        _cryptoProvider = cryptoProvider;
        _logger = logger;
    }

    /// <summary>
    /// Finds or creates a Jellyfin user based on Authentik user info, and syncs permissions.
    /// </summary>
    /// <param name="userInfo">The user info from Authentik.</param>
    /// <returns>The Jellyfin user ID.</returns>
    public async Task<Guid> SyncUserAsync(OidcUserInfo userInfo)
    {
        var config = Plugin.Instance!.Configuration;
        var username = userInfo.PreferredUsername;

        var user = _userManager.GetUserByName(username);

        if (user == null)
        {
            if (!config.AutoCreateUsers)
            {
                throw new UnauthorizedAccessException($"User '{username}' does not exist and auto-creation is disabled.");
            }

            _logger.LogInformation("Creating new Jellyfin user for Authentik user: {Username}", username);
            user = await _userManager.CreateUserAsync(username).ConfigureAwait(false);

            // Set a random password so the user cannot log in with local credentials
            user.Password = _cryptoProvider.CreatePasswordHash(
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))).ToString();
        }

        if (config.EnableGroupSync)
        {
            var isAdmin = userInfo.Groups.Contains(config.AdminGroup, StringComparer.OrdinalIgnoreCase);
            user.SetPermission(PermissionKind.IsAdministrator, isAdmin);

            _logger.LogDebug(
                "Synced permissions for {Username}: Admin={IsAdmin}",
                username,
                isAdmin);
        }

        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
        return user.Id;
    }

    /// <summary>
    /// Checks whether the user is authorized to log in based on group membership.
    /// </summary>
    /// <param name="userInfo">The user info from Authentik.</param>
    /// <returns>True if the user is allowed to log in.</returns>
    public bool IsAuthorized(OidcUserInfo userInfo)
    {
        var config = Plugin.Instance!.Configuration;

        // If no allowed group is configured, allow all authenticated users
        if (string.IsNullOrWhiteSpace(config.AllowedGroup))
        {
            return true;
        }

        return userInfo.Groups.Contains(config.AllowedGroup, StringComparer.OrdinalIgnoreCase)
            || userInfo.Groups.Contains(config.AdminGroup, StringComparer.OrdinalIgnoreCase);
    }
}
