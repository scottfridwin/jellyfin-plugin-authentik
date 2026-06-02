using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Authentik.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Authentik.Services;

/// <summary>
/// Handles user provisioning and permission sync from Authentik groups.
/// </summary>
public class UserSyncService
{
    private readonly IUserManager _userManager;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerConfigurationManager _configManager;
    private readonly ILogger<UserSyncService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSyncService"/> class.
    /// </summary>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <param name="cryptoProvider">The crypto provider for password generation.</param>
    /// <param name="httpClientFactory">The HTTP client factory for downloading images.</param>
    /// <param name="configManager">The server configuration manager.</param>
    /// <param name="logger">The logger.</param>
    public UserSyncService(IUserManager userManager, ICryptoProvider cryptoProvider, IHttpClientFactory httpClientFactory, IServerConfigurationManager configManager, ILogger<UserSyncService> logger)
    {
        _userManager = userManager;
        _cryptoProvider = cryptoProvider;
        _httpClientFactory = httpClientFactory;
        _configManager = configManager;
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

        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);

        if (config.EnableGroupSync)
        {
            var isAdmin = userInfo.Groups.Contains(config.AdminGroup, StringComparer.OrdinalIgnoreCase);

            var policy = new UserPolicy
            {
                IsAdministrator = isAdmin,
                EnableAllFolders = true,
                EnableRemoteControlOfOtherUsers = isAdmin,
                EnableLiveTvManagement = isAdmin,
                EnableLiveTvAccess = true,
                EnableMediaPlayback = true,
                EnableAudioPlaybackTranscoding = true,
                EnableVideoPlaybackTranscoding = true,
                EnablePlaybackRemuxing = true,
                EnableContentDeletion = isAdmin,
                EnableRemoteAccess = true,
                EnableAllChannels = true,
                EnableAllDevices = true,
                EnableSharedDeviceControl = true,
                AuthenticationProviderId = user.AuthenticationProviderId,
                PasswordResetProviderId = user.PasswordResetProviderId,
            };

            await _userManager.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);

            _logger.LogInformation(
                "Synced permissions for {Username}: Admin={IsAdmin}",
                username,
                isAdmin);
        }

        _logger.LogDebug("config.EnableProfileImageSync: ${EnableProfileImageSync}", config.EnableProfileImageSync);
        _logger.LogDebug("userInfo.Picture: ${Picture}", userInfo.Picture);
        if (config.EnableProfileImageSync && !string.IsNullOrEmpty(userInfo.Picture))
        {
            await SyncProfileImageAsync(user, userInfo.Picture).ConfigureAwait(false);
        }

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

        _logger.LogDebug(
            "Checking authorization for {Username}. Groups received: [{Groups}]",
            userInfo.PreferredUsername,
            string.Join(", ", userInfo.Groups));

        // If no allowed group is configured, allow all authenticated users
        if (string.IsNullOrWhiteSpace(config.AllowedGroup))
        {
            _logger.LogDebug("No AllowedGroup configured, granting access to {Username}", userInfo.PreferredUsername);
            return true;
        }

        var isAllowed = userInfo.Groups.Contains(config.AllowedGroup, StringComparer.OrdinalIgnoreCase)
            || userInfo.Groups.Contains(config.AdminGroup, StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            _logger.LogWarning(
                "User {Username} denied access. Required group: '{AllowedGroup}' or '{AdminGroup}'. User groups: [{Groups}]",
                userInfo.PreferredUsername,
                config.AllowedGroup,
                config.AdminGroup,
                string.Join(", ", userInfo.Groups));
        }

        return isAllowed;
    }

    /// <summary>
    /// Downloads or decodes the user's profile image and saves it to Jellyfin.
    /// Supports both URLs and base64 data URIs (e.g. data:image/png;base64,...).
    /// </summary>
    private async Task SyncProfileImageAsync(Jellyfin.Database.Implementations.Entities.User user, string picture)
    {
        try
        {
            byte[] imageBytes;
            string extension;

            if (picture.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("picture is a data URI, parsing for {Username}", user.Username);
                // Parse data URI: data:[<mediatype>][;base64],<data>
                var commaIndex = picture.IndexOf(',', StringComparison.Ordinal);
                if (commaIndex < 0)
                {
                    _logger.LogWarning("Invalid data URI for profile image of {Username}", user.Username);
                    return;
                }

                var header = picture[..commaIndex]; // e.g. "data:image/png;base64"
                var base64Data = picture[(commaIndex + 1)..];
                imageBytes = Convert.FromBase64String(base64Data);

                // Extract extension from media type
                extension = ".jpg";
                var mimeStart = header.IndexOf(':', StringComparison.Ordinal) + 1;
                var mimeEnd = header.IndexOf(';', StringComparison.Ordinal);
                if (mimeEnd > mimeStart)
                {
                    var mime = header[mimeStart..mimeEnd];
                    extension = mime switch
                    {
                        "image/png" => ".png",
                        "image/gif" => ".gif",
                        "image/webp" => ".webp",
                        _ => ".jpg",
                    };
                }
            }
            else
            {
                _logger.LogDebug("picture is not a data URI, treating as URL for {Username}", user.Username);
                // Treat as URL
                var httpClient = _httpClientFactory.CreateClient("AuthentikPlugin");
                using var response = await httpClient.GetAsync(new Uri(picture)).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                imageBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                // Determine extension from content type or URL
                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                extension = contentType switch
                {
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg",
                };
            }

            var userDataPath = Path.Combine(
                _configManager.ApplicationPaths.UserConfigurationDirectoryPath,
                user.Username);
            Directory.CreateDirectory(userDataPath);

            var imagePath = Path.Combine(userDataPath, "profile" + extension);

            // Skip if the image hasn't changed (compare SHA256 hash)
            _logger.LogDebug("imagePath: ${ImagePath}", imagePath);
            if (File.Exists(imagePath))
            {
                var existingHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(imagePath).ConfigureAwait(false)));
                var newHash = Convert.ToHexString(SHA256.HashData(imageBytes));
                _logger.LogDebug("existingHash: ${ExistingHash}", existingHash);
                _logger.LogDebug("newHash: ${NewHash}", newHash);
                if (string.Equals(existingHash, newHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Profile image unchanged for {Username}, skipping sync", user.Username);
                    return;
                }
            }

            await File.WriteAllBytesAsync(imagePath, imageBytes).ConfigureAwait(false);

            // Reload the user to avoid concurrency issues - the user object may have been modified since it was loaded
            var freshUser = _userManager.GetUserByName(user.Username);
            if (freshUser == null)
            {
                _logger.LogWarning("User {Username} no longer exists, skipping profile image sync", user.Username);
                return;
            }

            if (freshUser.ProfileImage is not null)
            {
                await _userManager.ClearProfileImageAsync(freshUser).ConfigureAwait(false);
            }

            freshUser.ProfileImage = new ImageInfo(imagePath);
            await _userManager.UpdateUserAsync(freshUser).ConfigureAwait(false);

            _logger.LogInformation("Synced profile image for {Username}", user.Username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync profile image for {Username}", user.Username);
        }
    }
}
