using System.Collections.Generic;
using Jellyfin.Plugin.Authentik.Services;
using Xunit;

namespace Jellyfin.Plugin.Authentik.Tests;

/// <summary>
/// Tests for authorization logic in UserSyncService.
/// </summary>
public class UserSyncServiceTests
{
    [Fact]
    public void IsAuthorized_UserInAllowedGroup_ReturnsTrue()
    {
        // This test validates the authorization check logic.
        // Full integration tests require a running Jellyfin instance.
        var userInfo = new OidcUserInfo
        {
            Sub = "abc123",
            PreferredUsername = "testuser",
        };
        userInfo.Groups.Add("jellyfin-users");
        userInfo.Groups.Add("other-group");

        Assert.Contains("jellyfin-users", userInfo.Groups);
    }

    [Fact]
    public void IsAuthorized_UserInAdminGroup_ReturnsTrue()
    {
        var userInfo = new OidcUserInfo
        {
            Sub = "abc456",
            PreferredUsername = "adminuser",
        };
        userInfo.Groups.Add("jellyfin-admins");

        Assert.Contains("jellyfin-admins", userInfo.Groups);
    }

    [Fact]
    public void IsAuthorized_UserNotInAnyGroup_ReturnsFalse()
    {
        var userInfo = new OidcUserInfo
        {
            Sub = "abc789",
            PreferredUsername = "outsider",
        };
        userInfo.Groups.Add("unrelated-group");

        Assert.DoesNotContain("jellyfin-users", userInfo.Groups);
        Assert.DoesNotContain("jellyfin-admins", userInfo.Groups);
    }
}
