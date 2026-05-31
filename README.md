# Jellyfin Plugin: Authentik SSO

Single sign-on authentication for [Jellyfin](https://jellyfin.org/) using [Authentik](https://goauthentik.io/) as the OpenID Connect identity provider.

## Features

- **OIDC authentication** via Authentik with PKCE (Proof Key for Code Exchange)
- **Automatic user provisioning** — creates Jellyfin users on first SSO login
- **Group-based permissions** — map Authentik groups to Jellyfin admin/user roles
- **Admin permission sync** — admin group members get full server management rights
- **Access control** — restrict Jellyfin access to specific Authentik groups
- **Reverse proxy support** — force HTTPS redirect URIs for TLS-terminating proxies
- **Themed callback page** — dark-by-default login interstitial that respects `prefers-color-scheme` and Jellyfin's Custom CSS
- **Minimal configuration** — only 3 required settings (URL, Client ID, Client Secret)

## Setup

### Authentik Side

1. Create an **OAuth2/OpenID Provider** in Authentik
   - **Authorization flow**: Use your standard authorization flow
   - **Client type**: Confidential
   - **Redirect URI**: `https://your-jellyfin-url/authentik/callback`
   - **Scopes**: `openid`, `profile`, `email` (ensure `groups` scope is included — it is by default)
2. Create an **Application** linked to the provider
3. Assign users/groups to the application

### Jellyfin Side

#### Installation via Plugin Repository (Recommended)

1. Go to **Dashboard → Plugins → Repositories**
2. Click **+** to add a new repository
3. Enter the repository URL:
   ```
   https://scottfridwin.github.io/jellyfin-plugin-authentik/manifest.json
   ```
4. Go to **Dashboard → Plugins → Catalog**
5. Search for **Authentik SSO** and click **Install**
6. Restart Jellyfin

> **Dev/testing builds:** To test pre-release builds, use this repository URL instead:
> ```
> https://scottfridwin.github.io/jellyfin-plugin-authentik/dev/manifest.json
> ```
> Dev builds are updated on every push to the `dev` branch and may be unstable.

#### Manual Installation

1. Download the latest release from [GitHub Releases](https://github.com/scottfridwin/jellyfin-plugin-authentik/releases)
2. Extract the zip file into your Jellyfin plugins directory:
   ```
   <jellyfin-data>/plugins/Jellyfin.Plugin.Authentik/
   ```
3. Restart Jellyfin

#### Configuration

After installation, go to **Dashboard → Plugins → Authentik SSO** and configure:

| Setting | Description | Default |
|---------|-------------|---------|
| **Authentik URL** | Your Authentik base URL (e.g., `https://auth.example.com`) | *(required)* |
| **Client ID** | From the Authentik OAuth2 provider | *(required)* |
| **Client Secret** | From the Authentik OAuth2 provider | *(required)* |
| **Admin Group** | Authentik group name for Jellyfin admins | `jellyfin-admins` |
| **Allowed Group** | Authentik group name for Jellyfin access | `jellyfin-users` |
| **Auto-Create Users** | Create Jellyfin accounts on first SSO login | `true` |
| **Enable Group Sync** | Sync Authentik groups → Jellyfin permissions on each login | `true` |
| **Force HTTPS Redirect** | Force `https://` in the OAuth callback URI (enable if behind a TLS-terminating reverse proxy) | `false` |

## Usage

Navigate to `https://your-jellyfin-url/authentik/start` to initiate SSO login.

### Login Button (Optional)

To add a "Sign in with Authentik" button on the login page, use two Jellyfin settings:

#### 1. Login Disclaimer (Dashboard → General → Login Disclaimer)

Paste this HTML:

```html
<form action="/authentik/start" class="sso-login-form">
  <button type="submit" class="sso-login-btn">
    Sign in with Authentik
  </button>
</form>
```

#### 2. Custom CSS (Dashboard → General → Custom CSS Code)

Paste this CSS (customize as needed):

```css
/* SSO Login Button */
.sso-login-form {
  margin-top: 1.5em;
  text-align: center;
}

.sso-login-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5em;
  padding: 0.75em 1.5em;
  width: 100%;
  max-width: 300px;
  background: #4051b5;
  color: #fff;
  border: none;
  border-radius: 4px;
  font-size: 1em;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.sso-login-btn:hover {
  background: #3444a3;
}

/* Optional: add a logo/icon before the text */
/* .sso-login-btn::before {
  content: '';
  display: inline-block;
  width: 20px;
  height: 20px;
  background: url('https://your-domain.com/logo.svg') no-repeat center/contain;
} */
```

#### Customization Examples

**Add a logo image inside the button** — modify the Login Disclaimer HTML:

```html
<form action="/authentik/start" class="sso-login-form">
  <button type="submit" class="sso-login-btn">
    <img src="https://your-domain.com/logo.svg" alt="" class="sso-login-logo">
    Sign in with Authentik
  </button>
</form>
```

Then add to Custom CSS:

```css
.sso-login-logo {
  width: 20px;
  height: 20px;
  object-fit: contain;
}
```

**Match your Authentik branding colors:**

```css
.sso-login-btn {
  background: #fd4b2d; /* Authentik orange */
}
.sso-login-btn:hover {
  background: #e0432a;
}
```

**Full-width button with rounded corners:**

```css
.sso-login-btn {
  max-width: 100%;
  border-radius: 2em;
  padding: 1em;
}
```

### Callback Page Styling

The intermediate "Completing login..." page uses a dark theme by default and respects `prefers-color-scheme`. It also loads Jellyfin's Custom CSS, so you can override its appearance:

```css
/* Override the SSO callback page */
.sso-container { background: #1a1a2e; }
.sso-spinner { border-top-color: #e94560; }
```

## How It Works

1. User clicks "Sign in with Authentik" (or navigates to `/authentik/start`)
2. Plugin generates a PKCE code verifier/challenge and redirects to Authentik
3. User authenticates in Authentik
4. Authentik redirects back to `/authentik/callback` with an authorization code
5. Plugin exchanges the code for tokens, fetches user info from Authentik
6. Plugin checks group membership for authorization
7. Plugin creates/updates the Jellyfin user and syncs permissions
8. A callback page completes the login client-side, storing the session in the browser
9. User is redirected to the Jellyfin home page

### Permission Sync

When **Enable Group Sync** is on, every login updates the user's Jellyfin permissions:

| Authentik Group | Jellyfin Permissions |
|-----------------|---------------------|
| Admin Group member | Full admin: manage server, delete content, remote control, live TV management |
| Allowed Group member (non-admin) | Standard user: playback, transcoding, remote access, all libraries |

## Development

### Prerequisites

- .NET 9.0 SDK

### Build

```bash
dotnet build
```

### Test

```bash
dotnet test
```

### Dev Container

Open in VS Code with the Dev Containers extension for a pre-configured development environment.

## License

MIT
