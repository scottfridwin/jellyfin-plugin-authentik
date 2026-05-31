using System;
using System.Collections.Concurrent;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.Authentik.Services;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Authentik.Api;

/// <summary>
/// API controller for Authentik SSO authentication flows.
/// </summary>
[ApiController]
[Route("[controller]")]
public class AuthentikController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, PendingAuth> PendingAuths = new();

    private readonly OidcService _oidcService;
    private readonly UserSyncService _userSyncService;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<AuthentikController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthentikController"/> class.
    /// </summary>
    /// <param name="oidcService">The OIDC service.</param>
    /// <param name="userSyncService">The user sync service.</param>
    /// <param name="sessionManager">The Jellyfin session manager.</param>
    /// <param name="logger">The logger.</param>
    public AuthentikController(
        OidcService oidcService,
        UserSyncService userSyncService,
        ISessionManager sessionManager,
        ILogger<AuthentikController> logger)
    {
        _oidcService = oidcService;
        _userSyncService = userSyncService;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the OIDC login flow by redirecting to Authentik.
    /// </summary>
    /// <returns>A redirect to the Authentik authorization endpoint.</returns>
    [HttpGet("start")]
    public ActionResult Start()
    {
        CleanupExpired();

        var state = Guid.NewGuid().ToString("N");
        var redirectUri = $"{GetBaseUrl()}/authentik/callback";

        var authUrl = _oidcService.GetAuthorizationUrl(redirectUri, state, out var codeVerifier);

        PendingAuths[state] = new PendingAuth(codeVerifier, DateTime.UtcNow);

        return Redirect(authUrl);
    }

    /// <summary>
    /// Handles the OIDC callback from Authentik after user authentication.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <param name="state">The state parameter for CSRF validation.</param>
    /// <returns>An HTML page that completes the client-side authentication.</returns>
    [HttpGet("callback")]
    public async Task<ActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(state) || !PendingAuths.TryRemove(state, out var pending))
        {
            return BadRequest("Invalid or expired state.");
        }

        if (DateTime.UtcNow - pending.Created > TimeSpan.FromMinutes(5))
        {
            return BadRequest("Authentication request expired.");
        }

        var redirectUri = $"{GetBaseUrl()}/authentik/callback";
        var tokenResponse = await _oidcService.ExchangeCodeAsync(code, redirectUri, pending.CodeVerifier).ConfigureAwait(false);

        if (tokenResponse == null)
        {
            return Problem("Failed to exchange authorization code for tokens.");
        }

        var userInfo = await _oidcService.GetUserInfoAsync(tokenResponse.AccessToken).ConfigureAwait(false);

        if (userInfo == null)
        {
            return Problem("Failed to retrieve user information from Authentik.");
        }

        _logger.LogInformation(
            "OIDC callback for user {Username}, groups: [{Groups}]",
            userInfo.PreferredUsername,
            string.Join(", ", userInfo.Groups));

        if (!_userSyncService.IsAuthorized(userInfo))
        {
            return Unauthorized("You are not authorized to access Jellyfin. Check your Authentik group membership.");
        }

        var userId = await _userSyncService.SyncUserAsync(userInfo).ConfigureAwait(false);

        // Store the user ID for the client-side auth completion
        var completionState = Guid.NewGuid().ToString("N");
        PendingAuths[completionState] = new PendingAuth(string.Empty, DateTime.UtcNow) { UserId = userId, Username = userInfo.PreferredUsername };

        return Content(GenerateCallbackHtml(completionState), MediaTypeNames.Text.Html);
    }

    /// <summary>
    /// Completes authentication by creating a Jellyfin session from the client-side callback.
    /// </summary>
    /// <param name="request">The client device information and completion state.</param>
    /// <returns>The Jellyfin authentication result.</returns>
    [HttpPost("auth")]
    [Consumes(MediaTypeNames.Application.Json)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<ActionResult> Authenticate([FromBody] AuthenticateRequest request)
    {
        if (string.IsNullOrEmpty(request.State) || !PendingAuths.TryRemove(request.State, out var pending))
        {
            return BadRequest("Invalid or expired authentication state.");
        }

        if (pending.UserId == Guid.Empty)
        {
            return Problem("No user associated with this authentication state.");
        }

        var authRequest = new AuthenticationRequest
        {
            UserId = pending.UserId,
            Username = pending.Username ?? string.Empty,
            App = request.AppName ?? "Jellyfin Web",
            AppVersion = request.AppVersion ?? "0.0.0",
            DeviceId = request.DeviceId ?? Guid.NewGuid().ToString(),
            DeviceName = request.DeviceName ?? "SSO Login",
        };

        var result = await _sessionManager.AuthenticateDirect(authRequest).ConfigureAwait(false);
        return Ok(result);
    }

    private static string GenerateCallbackHtml(string state)
    {
        var template = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Authentik SSO - Completing login...</title>
                <link rel="stylesheet" href="/web/custom.css" type="text/css">
                <style>
                    :root {
                        --sso-bg: #101010;
                        --sso-text: #d1cfce;
                        --sso-accent: #00a4dc;
                    }
                    @media (prefers-color-scheme: light) {
                        :root {
                            --sso-bg: #f0f0f0;
                            --sso-text: #333;
                            --sso-accent: #00a4dc;
                        }
                    }
                    * { margin: 0; padding: 0; box-sizing: border-box; }
                    body {
                        background: var(--sso-bg);
                        color: var(--sso-text);
                        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        min-height: 100vh;
                    }
                    .sso-container {
                        text-align: center;
                        padding: 2rem;
                    }
                    .sso-spinner {
                        width: 40px;
                        height: 40px;
                        border: 3px solid var(--sso-text);
                        border-top-color: var(--sso-accent);
                        border-radius: 50%;
                        animation: spin 0.8s linear infinite;
                        margin: 0 auto 1rem;
                    }
                    @keyframes spin { to { transform: rotate(360deg); } }
                    .sso-error { color: #f44336; margin-top: 1rem; }
                </style>
            </head>
            <body>
                <div class="sso-container">
                    <div class="sso-spinner"></div>
                    <p>Completing login, please wait...</p>
                </div>
                <script>
                    const state = '__STATE__';
                    const baseUrl = window.location.origin;
                    fetch(baseUrl + '/authentik/auth', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            state: state,
                            deviceId: localStorage.getItem('_deviceId2') || crypto.randomUUID(),
                            deviceName: navigator.userAgent.substring(0, 50),
                            appName: 'Jellyfin Web',
                            appVersion: '10.11.0'
                        })
                    })
                    .then(r => r.json())
                    .then(data => {
                        const server = {
                            ManualAddress: window.location.origin,
                            Id: data.ServerId,
                            AccessToken: data.AccessToken,
                            UserId: data.User.Id,
                            Name: window.location.hostname
                        };
                        const credentials = { Servers: [server] };
                        localStorage.setItem('jellyfin_credentials', JSON.stringify(credentials));
                        localStorage.setItem('_jellyfin_credentials', JSON.stringify(credentials));
                        window.location.href = '/web/#/home.html';
                    })
                    .catch(err => {
                        document.querySelector('.sso-spinner').style.display = 'none';
                        document.querySelector('.sso-container').innerHTML +=
                            '<p class="sso-error">Login failed: ' + err.message + '</p>';
                    });
                </script>
            </body>
            </html>
            """;

        return template.Replace("__STATE__", state, StringComparison.Ordinal);
    }

    private void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        foreach (var kvp in PendingAuths)
        {
            if (kvp.Value.Created < cutoff)
            {
                PendingAuths.TryRemove(kvp.Key, out _);
            }
        }
    }

    private string GetBaseUrl()
    {
        var config = Plugin.Instance!.Configuration;
        var scheme = config.ForceHttpsRedirect ? "https" : Request.Scheme;

        var port = Request.Host.Port ?? -1;
        if ((port == 80 && scheme == "http") || (port == 443 && scheme == "https"))
        {
            port = -1;
        }

        return new UriBuilder
        {
            Scheme = scheme,
            Host = Request.Host.Host,
            Port = port,
            Path = Request.PathBase,
        }.ToString().TrimEnd('/');
    }

    private sealed record PendingAuth(string CodeVerifier, DateTime Created)
    {
        public Guid UserId { get; set; }

        public string? Username { get; set; }
    }
}
