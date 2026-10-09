# Authentik SSO for Jellyfin

[![Build](https://img.shields.io/github/actions/workflow/status/scottfridwin/jellyfin-plugin-authentik/build.yaml?branch=main&label=build)](https://github.com/scottfridwin/jellyfin-plugin-authentik/actions/workflows/build.yaml)
[![Release](https://img.shields.io/github/v/release/scottfridwin/jellyfin-plugin-authentik)](https://github.com/scottfridwin/jellyfin-plugin-authentik/releases/latest)
[![Jellyfin](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fscottfridwin%2Fjellyfin-plugin-authentik%2Fmain%2Fbuild.yaml&query=%24.targetAbi&label=Jellyfin&logo=jellyfin&color=00a4dc)](https://jellyfin.org/)
[![Downloads](https://img.shields.io/github/downloads/scottfridwin/jellyfin-plugin-authentik/total)](https://github.com/scottfridwin/jellyfin-plugin-authentik/releases)
[![License](https://img.shields.io/github/license/scottfridwin/jellyfin-plugin-authentik)](LICENSE)

Sign in to [Jellyfin](https://jellyfin.org/) with your [Authentik](https://goauthentik.io/) account. Users are created on first login, and their Authentik groups decide whether they can get in, whether they are admins, and which content ratings they can see.

> [!NOTE]
> **AI disclosure:** This project is built and maintained with substantial help from AI coding assistants (GitHub Copilot). AI is used to write and modify the code, tests, documentation and CI configuration, and to manage the repository. Dependency updates are merged and released automatically, without human review, when the automated tests pass. Review the code and test it in your own environment before relying on it for access control.

## Features

- **Single sign-on** through Authentik (OpenID Connect with PKCE)
- **Automatic user creation** on first login
- **Access control** — only members of an Authentik group can sign in
- **Admin mapping** — members of an Authentik group become Jellyfin administrators
- **Parental controls** — map Authentik groups to maximum content ratings (G, PG, PG-13, TV-14, …)
- **Profile pictures** synced from Authentik
- **Works behind a reverse proxy**

## Requirements

- Jellyfin — the badge above shows the version the latest release targets; the plugin catalog automatically offers the newest release that is compatible with your server
- Authentik with permission to create an OAuth2/OpenID provider

## Installation

1. In Jellyfin, open **Dashboard → Plugins → Repositories** and add:

   ```text
   https://scottfridwin.github.io/jellyfin-plugin-authentik/manifest.json
   ```

2. Open **Dashboard → Plugins → Catalog**, install **Authentik SSO**, and restart Jellyfin.

<details>
<summary>Manual installation</summary>

Download the zip for your Jellyfin version from [Releases](https://github.com/scottfridwin/jellyfin-plugin-authentik/releases), extract it to `<jellyfin-data>/plugins/Jellyfin.Plugin.Authentik/`, and restart Jellyfin. The repository method above is preferred because Jellyfin then installs updates for you.

</details>

## Setup

### 1. Create the provider in Authentik

1. **Applications → Providers → Create → OAuth2/OpenID Provider**
   - **Client type:** Confidential
   - **Redirect URI (strict):** `https://jellyfin.example.com/authentik/callback` (if Jellyfin uses a **Base URL**, include it, e.g. `https://example.com/jellyfin/authentik/callback`)
   - Keep the default scopes (`openid`, `email`, `profile`). The default `profile` mapping includes the user's groups, which the plugin needs.
2. **Applications → Applications → Create**, link it to the provider, and bind the users or groups who should see it.
3. Create the groups you want to use, for example `jellyfin-users` and `jellyfin-admins`.

Note the **Client ID** and **Client Secret** from the provider.

### 2. Configure the plugin

Open **Dashboard → Plugins → Authentik SSO**:

| Setting | Description | Default |
| --- | --- | --- |
| Authentik URL | Base URL of Authentik, e.g. `https://auth.example.com` | *required* |
| Client ID / Client Secret | From the Authentik provider | *required* |
| Admin Group | Members become Jellyfin administrators | `jellyfin-admins` |
| Required Group | Members (and admins) may sign in. Leave blank to allow every Authentik user who can reach the application | `jellyfin-users` |
| Force HTTPS in redirect URI | Enable when a reverse proxy terminates TLS and Authentik reports a `redirect_uri` mismatch | off |
| Auto-create users | Create a Jellyfin account on first login | on |
| Sync groups to permissions | Re-apply admin/standard permissions on every login ([details](docs/permissions.md#permission-sync)) | on |
| Sync groups to content ratings | Apply parental rating limits from the rating groups below ([details](docs/permissions.md#content-rating-restrictions)) | off |
| G / TV-Y7 / PG / PG-13 / TV-14 Group | Optional groups that cap a user's maximum rating | *empty* |
| Sync profile image | Copy the user's avatar from Authentik on every login | on |
| Profile Image Claim Path | Where to find the image URL in the userinfo response | `picture` |

### 3. Add a login button

Users sign in at `https://jellyfin.example.com/authentik/login` (plus your Base URL, if any). To show a button on the Jellyfin login page, paste this into **Dashboard → General → Login disclaimer**:

```html
<form action="../authentik/login" class="sso-login-form">
  <button type="submit" class="sso-login-btn">Sign in with Authentik</button>
</form>
```

The relative `../authentik/login` path works whether or not Jellyfin uses a Base URL.

…and this into **Dashboard → General → Custom CSS code**:

```css
.sso-login-form { margin-top: 1.5em; text-align: center; }
.sso-login-btn {
  width: 100%; max-width: 300px; padding: 0.75em 1.5em;
  background: #4051b5; color: #fff; border: none; border-radius: 4px;
  font-size: 1em; cursor: pointer;
}
.sso-login-btn:hover { background: #3444a3; }
```

More styling options are in [docs/customization.md](docs/customization.md).

### Optional: profile pictures

Authentik does not send avatars by default. Add a scope mapping under **Customization → Property Mappings → Create → Scope Mapping** with scope name `profile` and this expression, then select it on the provider:

```python
return {"picture": request.user.avatar}
```

If you keep avatars in a user attribute instead, see [docs/customization.md](docs/customization.md#profile-pictures-from-a-custom-attribute).

## Good to know

- **Accounts are matched by username.** An Authentik user whose `preferred_username` matches an existing Jellyfin user signs in to that account, and (with permission sync on) that account's admin status and permissions are then managed by Authentik groups.
- **SSO users cannot use a local password.** Auto-created accounts get a random password. Keep a local administrator account as a fallback in case Authentik is unavailable.
- **Apps with a native login screen** (Android TV, Swiftfin, Findroid, Kodi, …) cannot open the SSO page. Sign in on the web, then authorize the app with **Quick Connect** (enable it under **Dashboard → General**).

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Authentik shows *redirect_uri mismatch* | The redirect URI in Authentik must exactly match `https://<your-jellyfin-host>[/<base-url>]/authentik/callback`. Behind a TLS-terminating proxy, enable **Force HTTPS in redirect URI** and make sure the proxy forwards the original `Host` header. |
| *You are not authorized to access Jellyfin* | The user is in neither the Required Group nor the Admin Group, or the provider is not sending the `groups` claim (keep the default `profile` scope mapping). |
| *Login failed* after returning from Authentik | Check the Jellyfin log for lines from `Jellyfin.Plugin.Authentik`. |

## Further reading

- [Permissions and content ratings](docs/permissions.md)
- [Customization](docs/customization.md)
- [How it works](docs/how-it-works.md)
- [Development](docs/development.md)

## License

[GPL-3.0](LICENSE)
