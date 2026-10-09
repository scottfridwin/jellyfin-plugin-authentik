# Development

## Prerequisites

- .NET SDK version from [`global.json`](../global.json) (or open the repository in the included dev container)

## Build and test

```bash
dotnet build
dotnet test
```

Tests use xUnit v3 on Microsoft.Testing.Platform (enabled in `global.json`).

To try a build on a server, run `dotnet publish Jellyfin.Plugin.Authentik -c Release -o artifacts`, copy `artifacts/` into the Jellyfin plugins directory and restart Jellyfin. Every CI run also uploads the built plugin as a workflow artifact.

## Project layout

| Path | Contents |
| --- | --- |
| `Jellyfin.Plugin.Authentik/Api` | HTTP endpoints (`/authentik/*`) |
| `Jellyfin.Plugin.Authentik/Services` | OIDC client and user/permission sync |
| `Jellyfin.Plugin.Authentik/Configuration` | Settings model and dashboard page |
| `build.yaml` | Plugin metadata, including `targetAbi` |

## Continuous integration and releases

All changes reach `main` through a pull request; the **Test** check must pass and merges are squashed.

- **Dependencies** are updated by [Renovate](https://docs.renovatebot.com/) ([`renovate.json`](../renovate.json)). Updates wait 3 days after publication, then merge automatically once **Test** passes. GitHub Actions are pinned to commit SHAs, and the repository rejects unpinned actions.
- **Jellyfin packages** (`Jellyfin.Controller`, `Jellyfin.Model`, `Jellyfin.Data`) are updated together with `targetAbi` in `build.yaml`. CI fails if `targetAbi` does not match the package version.
- **Releases** are created automatically when a Renovate update changes the shipped plugin: the third part of the latest `vA.B.C.D` tag is incremented, a GitHub release is published, and the catalog on `gh-pages` is updated. For other changes, run the **Build and Release Plugin** workflow manually with a `release_tag` such as `v2.1.0.0`.
- If an unattended run on `main` fails, an issue titled **CI failed on main** is opened.
