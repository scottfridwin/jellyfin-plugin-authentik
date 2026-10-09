using Jellyfin.Plugin.Authentik.Api;
using Xunit;

namespace Jellyfin.Plugin.Authentik.Tests;

/// <summary>
/// Tests for the login completion page returned by the callback endpoint.
/// </summary>
public class CallbackPageTests
{
    [Fact]
    public void WithoutBaseUrl_UsesRootPaths()
    {
        var html = AuthentikController.GenerateCallbackHtml("abc123", string.Empty);

        Assert.Contains("href=\"/web/custom.css\"", html, System.StringComparison.Ordinal);
        Assert.Contains("const basePath = \"\";", html, System.StringComparison.Ordinal);
        Assert.Contains("const state = 'abc123';", html, System.StringComparison.Ordinal);
    }

    [Fact]
    public void WithBaseUrl_PrefixesAllJellyfinPaths()
    {
        var html = AuthentikController.GenerateCallbackHtml("abc123", "/jellyfin");

        Assert.Contains("href=\"/jellyfin/web/custom.css\"", html, System.StringComparison.Ordinal);
        Assert.Contains("const basePath = \"/jellyfin\";", html, System.StringComparison.Ordinal);
        Assert.Contains("fetch(serverUrl + '/authentik/auth'", html, System.StringComparison.Ordinal);
        Assert.Contains("ManualAddress: serverUrl", html, System.StringComparison.Ordinal);
        Assert.Contains("basePath + '/web/#/home.html'", html, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BaseUrl_IsEncoded()
    {
        var html = AuthentikController.GenerateCallbackHtml("abc123", "/x\"</script><script>alert(1)</script>");

        Assert.DoesNotContain("<script>alert(1)", html, System.StringComparison.Ordinal);
        Assert.DoesNotContain("\"</script>", html, System.StringComparison.Ordinal);
    }
}
