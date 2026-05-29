# Jellyfin Plugin: Authentik SSO

Single sign-on authentication for [Jellyfin](https://jellyfin.org/) using [Authentik](https://goauthentik.io/) as the OpenID Connect identity provider.

## Features

- **OIDC authentication** via Authentik with PKCE
- **Automatic user provisioning** on first login
- **Group-based permissions** — map Authentik groups to Jellyfin admin/user roles
- **Minimal configuration** — only 5 settings required

## Setup

### Authentik Side

1. Create an **OAuth2/OpenID Provider** in Authentik
2. Set the redirect URI to: `https://your-jellyfin-url/authentik/callback`
3. Create an **Application** linked to the provider
4. Ensure the `groups` scope is included (it is by default)

### Jellyfin Side

1. Install the plugin
2. Go to **Dashboard → Plugins → Authentik SSO**
3. Configure:
   - **Authentik URL**: Your Authentik base URL (e.g., `https://auth.example.com`)
   - **Client ID**: From the Authentik provider
   - **Client Secret**: From the Authentik provider
   - **Admin Group**: Authentik group for Jellyfin admins (default: `jellyfin-admins`)
   - **Allowed Group**: Authentik group for Jellyfin access (default: `jellyfin-users`)

## Usage

Navigate to `https://your-jellyfin-url/authentik/start` to initiate SSO login.

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
