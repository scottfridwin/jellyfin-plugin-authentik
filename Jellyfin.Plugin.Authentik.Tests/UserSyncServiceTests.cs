using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Authentik.Configuration;
using Jellyfin.Plugin.Authentik.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Authentik.Tests;

/// <summary>
/// Tests for UserSyncService — authorization checks, user provisioning, and permission sync.
/// </summary>
public class UserSyncServiceTests : IDisposable
{
    private readonly Mock<IUserManager> _mockUserManager;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ILogger<UserSyncService> _logger;
    private readonly UserSyncService _service;
    private readonly PluginConfiguration _config;

    public UserSyncServiceTests()
    {
        _mockUserManager = new Mock<IUserManager>();
        _cryptoProvider = new FakeCryptoProvider();
        _logger = NullLogger<UserSyncService>.Instance;
        _service = new UserSyncService(_mockUserManager.Object, _cryptoProvider, _logger);

        _config = new PluginConfiguration
        {
            AdminGroup = "jellyfin-admins",
            AllowedGroup = "jellyfin-users",
            AutoCreateUsers = true,
            EnableGroupSync = true,
        };

        SetPluginConfiguration(_config);
    }

    public void Dispose()
    {
        // Reset Plugin.Instance to avoid test pollution
        SetPluginInstance(null);
        GC.SuppressFinalize(this);
    }

    // ----- IsAuthorized Tests -----

    [Fact]
    public void IsAuthorized_UserInAllowedGroup_ReturnsTrue()
    {
        var userInfo = CreateUserInfo("testuser", "jellyfin-users");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_UserInAdminGroup_ReturnsTrue()
    {
        var userInfo = CreateUserInfo("adminuser", "jellyfin-admins");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_UserInBothGroups_ReturnsTrue()
    {
        var userInfo = CreateUserInfo("superuser", "jellyfin-users", "jellyfin-admins");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_UserNotInAnyRequiredGroup_ReturnsFalse()
    {
        var userInfo = CreateUserInfo("outsider", "unrelated-group", "another-group");

        Assert.False(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_UserWithNoGroups_ReturnsFalse()
    {
        var userInfo = CreateUserInfo("lonely");

        Assert.False(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_EmptyAllowedGroup_AllowsEveryone()
    {
        _config.AllowedGroup = string.Empty;
        var userInfo = CreateUserInfo("anyone", "random-group");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_NullAllowedGroup_AllowsEveryone()
    {
        _config.AllowedGroup = null!;
        var userInfo = CreateUserInfo("anyone");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_GroupMatchIsCaseInsensitive()
    {
        var userInfo = CreateUserInfo("mixedcase", "Jellyfin-Users");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    [Fact]
    public void IsAuthorized_AdminGroupMatchIsCaseInsensitive()
    {
        var userInfo = CreateUserInfo("admin", "JELLYFIN-ADMINS");

        Assert.True(_service.IsAuthorized(userInfo));
    }

    // ----- SyncUserAsync Tests -----

    [Fact]
    public async Task SyncUserAsync_ExistingUser_DoesNotCreateNew()
    {
        var existingUser = CreateMockUser("testuser");
        _mockUserManager.Setup(m => m.GetUserByName("testuser")).Returns(existingUser);

        var userInfo = CreateUserInfo("testuser", "jellyfin-users");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.CreateUserAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SyncUserAsync_NewUser_CreatesUser()
    {
        _mockUserManager.Setup(m => m.GetUserByName("newuser")).Returns((User?)null);
        var newUser = CreateMockUser("newuser");
        _mockUserManager.Setup(m => m.CreateUserAsync("newuser")).ReturnsAsync(newUser);

        var userInfo = CreateUserInfo("newuser", "jellyfin-users");
        var userId = await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.CreateUserAsync("newuser"), Times.Once);
        Assert.Equal(newUser.Id, userId);
    }

    [Fact]
    public async Task SyncUserAsync_NewUser_AutoCreateDisabled_Throws()
    {
        _config.AutoCreateUsers = false;
        _mockUserManager.Setup(m => m.GetUserByName("newuser")).Returns((User?)null);

        var userInfo = CreateUserInfo("newuser", "jellyfin-users");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SyncUserAsync(userInfo));
    }

    [Fact]
    public async Task SyncUserAsync_AdminUser_UpdatesPolicyWithAdminPermissions()
    {
        var user = CreateMockUser("admin");
        _mockUserManager.Setup(m => m.GetUserByName("admin")).Returns(user);

        var userInfo = CreateUserInfo("admin", "jellyfin-admins");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdatePolicyAsync(
            user.Id,
            It.Is<UserPolicy>(p =>
                p.IsAdministrator == true &&
                p.EnableContentDeletion == true &&
                p.EnableRemoteControlOfOtherUsers == true &&
                p.EnableLiveTvManagement == true &&
                p.EnableAllFolders == true)),
            Times.Once);
    }

    [Fact]
    public async Task SyncUserAsync_NonAdminUser_UpdatesPolicyWithoutAdminPermissions()
    {
        var user = CreateMockUser("regular");
        _mockUserManager.Setup(m => m.GetUserByName("regular")).Returns(user);

        var userInfo = CreateUserInfo("regular", "jellyfin-users");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdatePolicyAsync(
            user.Id,
            It.Is<UserPolicy>(p =>
                p.IsAdministrator == false &&
                p.EnableContentDeletion == false &&
                p.EnableRemoteControlOfOtherUsers == false &&
                p.EnableLiveTvManagement == false &&
                p.EnableAllFolders == true &&
                p.EnableMediaPlayback == true &&
                p.EnableRemoteAccess == true)),
            Times.Once);
    }

    [Fact]
    public async Task SyncUserAsync_GroupSyncDisabled_DoesNotUpdatePolicy()
    {
        _config.EnableGroupSync = false;
        var user = CreateMockUser("testuser");
        _mockUserManager.Setup(m => m.GetUserByName("testuser")).Returns(user);

        var userInfo = CreateUserInfo("testuser", "jellyfin-admins");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdatePolicyAsync(It.IsAny<Guid>(), It.IsAny<UserPolicy>()), Times.Never);
    }

    [Fact]
    public async Task SyncUserAsync_PreservesAuthenticationProviderId()
    {
        var user = CreateMockUser("testuser");
        user.AuthenticationProviderId = "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";
        user.PasswordResetProviderId = "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider";
        _mockUserManager.Setup(m => m.GetUserByName("testuser")).Returns(user);

        var userInfo = CreateUserInfo("testuser", "jellyfin-users");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdatePolicyAsync(
            user.Id,
            It.Is<UserPolicy>(p =>
                p.AuthenticationProviderId == "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider" &&
                p.PasswordResetProviderId == "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider")),
            Times.Once);
    }

    [Fact]
    public async Task SyncUserAsync_NewUser_SetsRandomPassword()
    {
        _mockUserManager.Setup(m => m.GetUserByName("newuser")).Returns((User?)null);
        var newUser = CreateMockUser("newuser");
        _mockUserManager.Setup(m => m.CreateUserAsync("newuser")).ReturnsAsync(newUser);

        var userInfo = CreateUserInfo("newuser", "jellyfin-users");
        await _service.SyncUserAsync(userInfo);

        // After sync, the user should have a password set (not the default empty)
        Assert.NotNull(newUser.Password);
        Assert.NotEmpty(newUser.Password);
    }

    [Fact]
    public async Task SyncUserAsync_UserInBothAdminAndAllowedGroup_GetsAdminPermissions()
    {
        var user = CreateMockUser("both");
        _mockUserManager.Setup(m => m.GetUserByName("both")).Returns(user);

        var userInfo = CreateUserInfo("both", "jellyfin-users", "jellyfin-admins");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdatePolicyAsync(
            user.Id,
            It.Is<UserPolicy>(p => p.IsAdministrator == true)),
            Times.Once);
    }

    [Fact]
    public async Task SyncUserAsync_AdminDemotedFromGroup_LosesAdminOnNextLogin()
    {
        var user = CreateMockUser("demoted");
        _mockUserManager.Setup(m => m.GetUserByName("demoted")).Returns(user);

        // User is only in allowed group now (was admin before)
        var userInfo = CreateUserInfo("demoted", "jellyfin-users");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdatePolicyAsync(
            user.Id,
            It.Is<UserPolicy>(p => p.IsAdministrator == false)),
            Times.Once);
    }

    [Fact]
    public async Task SyncUserAsync_CallsUpdateUserAsync()
    {
        var user = CreateMockUser("testuser");
        _mockUserManager.Setup(m => m.GetUserByName("testuser")).Returns(user);

        var userInfo = CreateUserInfo("testuser", "jellyfin-users");
        await _service.SyncUserAsync(userInfo);

        _mockUserManager.Verify(m => m.UpdateUserAsync(user), Times.Once);
    }

    [Fact]
    public async Task SyncUserAsync_ReturnsUserId()
    {
        var user = CreateMockUser("testuser");
        _mockUserManager.Setup(m => m.GetUserByName("testuser")).Returns(user);

        var userInfo = CreateUserInfo("testuser", "jellyfin-users");
        var result = await _service.SyncUserAsync(userInfo);

        Assert.Equal(user.Id, result);
    }

    // ----- Helpers -----

    private static OidcUserInfo CreateUserInfo(string username, params string[] groups)
    {
        var userInfo = new OidcUserInfo
        {
            Sub = Guid.NewGuid().ToString(),
            PreferredUsername = username,
        };
        foreach (var g in groups)
        {
            userInfo.Groups.Add(g);
        }

        return userInfo;
    }

    private static User CreateMockUser(string username)
    {
        return new User(
            username,
            "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider",
            "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider");
    }

    /// <summary>
    /// Fake ICryptoProvider since Moq cannot proxy ReadOnlySpan parameters.
    /// </summary>
    private sealed class FakeCryptoProvider : ICryptoProvider
    {
        public string DefaultHashMethod => "SHA256";

        public IEnumerable<string> GetSupportedHashMethods() => new[] { "SHA256" };

        public byte[] ComputeHash(string hashMethod, byte[] bytes, byte[] salt)
            => new byte[] { 1, 2, 3 };

        public byte[] ComputeHashWithDefaultMethod(byte[] bytes, byte[] salt)
            => new byte[] { 1, 2, 3 };

        public byte[] GenerateSalt() => new byte[] { 4, 5, 6 };
        public byte[] GenerateSalt(int length) => new byte[length];

        public PasswordHash CreatePasswordHash(ReadOnlySpan<char> password)
            => new PasswordHash("SHA256", new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 });

        public bool Verify(PasswordHash hash, ReadOnlySpan<char> password) => true;
    }

    private static void SetPluginConfiguration(PluginConfiguration config)
    {
        // Use reflection to set Plugin.Instance with a real-ish Plugin
        // that has our test configuration
        var instanceProp = typeof(Plugin).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

        // Create a Plugin using FormatterServices (bypasses constructor) to avoid
        // needing real IApplicationPaths/IXmlSerializer
        var plugin = (Plugin)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Plugin));

        // Set the Configuration property via the base class backing field
        var configProp = typeof(Plugin).BaseType!.GetProperty("Configuration");
        configProp!.SetValue(plugin, config);

        instanceProp!.SetValue(null, plugin);
    }

    private static void SetPluginInstance(Plugin? instance)
    {
        var prop = typeof(Plugin).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        prop!.SetValue(null, instance);
    }
}
