# Permissions and content ratings

Both features read the `groups` claim that Authentik sends on every login. Group names are compared case-insensitively.

## Who can sign in

A user may sign in when they are a member of the **Required Group** *or* the **Admin Group**. If **Required Group** is blank, every Authentik user who is allowed to use the application can sign in.

## Permission sync

When **Sync groups to permissions** is on, the plugin overwrites the user's Jellyfin permissions on every login:

| Permission | Admin Group member | Everyone else |
| --- | --- | --- |
| Administrator | ✔ | ✘ |
| Delete content | ✔ | ✘ |
| Remote control other users | ✔ | ✘ |
| Manage Live TV | ✔ | ✘ |
| Access to all libraries, channels and devices | ✔ | ✔ |
| Playback, transcoding and remuxing | ✔ | ✔ |
| Live TV access | ✔ | ✔ |
| Remote (internet) access | ✔ | ✔ |

> [!IMPORTANT]
> Changes made to these permissions in the Jellyfin dashboard are reverted at the user's next login. Turn permission sync off if you prefer to manage permissions in Jellyfin.

## Content rating restrictions

When **Sync groups to content ratings** is on, users in a rating group are limited to that rating and below:

| Setting | Allows |
| --- | --- |
| G / TV-G / TV-Y Group | G, TV-G, TV-Y |
| TV-Y7 Group | TV-Y7 and below |
| PG / TV-PG Group | PG, TV-PG and below |
| PG-13 Group | PG-13 and below |
| TV-14 Group | TV-14 and below |

Ratings use Jellyfin's built-in normalization, so equivalent movie and TV ratings share a threshold (`PG` and `TV-PG` are the same level; `PG-13` and `TV-14` are not).

Rules:

- Members of the **Admin Group** are never restricted.
- Users in no rating group are not restricted.
- A user in several rating groups gets the **least** restrictive one.
- Restricted users also cannot see **unrated** content.
- If permission sync is off and the plugin cannot read the user's existing Jellyfin policy, the login is refused rather than risk granting unrestricted access.

### Example

With groups `jellyfin-users`, `jellyfin-admins`, `jellyfin-rating-g`, `jellyfin-rating-pg` and `jellyfin-rating-tv14` configured:

| User's groups | Result |
| --- | --- |
| `jellyfin-users` | Standard user, no rating limit |
| `jellyfin-users`, `jellyfin-rating-g` | G, TV-G and TV-Y only |
| `jellyfin-users`, `jellyfin-rating-pg` | PG, TV-PG and below |
| `jellyfin-users`, `jellyfin-rating-pg`, `jellyfin-rating-tv14` | TV-14 and below |
| `jellyfin-admins`, `jellyfin-rating-g` | Administrator, no rating limit |
