# CLAUDE.md

Two apps live in this repo. Both stream live IPTV channels from M3U/M3U8 playlists and share the same feature set and data model (playlists, categories, favorites). Keep behavior in sync when changing one.

| App | Folder | Stack | Target |
|---|---|---|---|
| TVapp | `TVapp/` | Vanilla JS/HTML/CSS, hls.js | Samsung Tizen smart TV (remote-control driven) |
| MunIPTV | `MunIPTV/` | .NET 10 MAUI, `net10.0-android` only | Android phones/tablets (touch, landscape-locked) |

## Folder structure

```
README.md              User docs for both apps
CLAUDE.md              This file
MunIPTV.sln            Solution for the MAUI app (contains only MunIPTV/MunIPTV.csproj)

TVapp/                 Tizen web app. Build/package from inside this folder.
  config.xml             Widget manifest (app id, privileges, tv-samsung profile)
  index.html             App shell; every screen is a <section class="screen">
  css/style.css          All styling, including the .focused states
  js/main.js             All app logic (single DOMContentLoaded closure)
  playlists/index.country.m3u   Bundled default playlist (offline fallback)
  images/                Donate art/QR, thumbnail, icons
  icon.png, noun-live-tv-3548799.png   Icons (the latter is referenced by config.xml)
  TVapp.wgt              Pre-built signed package (committed binary)

MunIPTV/               .NET MAUI Android app
  MunIPTV.csproj         Single-project MAUI, net10.0-android only
  MauiProgram.cs         DI registration + CommunityToolkit setup
  App.xaml(.cs)          Theme resources (dark), CreateWindow -> AppShell
  AppShell.xaml(.cs)     TabBar: Home / Categories / Support / Settings; registers "channels" and "player" routes
  Models/                Playlist, Channel, Category (+ CategoryGroup for grouped lists)
  Services/              M3UParser, PlaylistService (all state), PlayerNav, Ui (toast)
  Pages/                 HomePage, CategoriesPage, ChannelsPage, SettingsPage, SupportPage, PlayerPage
  Platforms/Android/     AndroidManifest.xml, MainActivity (landscape lock), MainApplication
  Resources/             AppIcon, Splash, Images (donate QR), Raw/index.country.m3u (bundled playlist copy)
```

Keep the two apps' files apart: `TVapp/` is packaged wholesale by `tizen build-web -- .`, so nothing from `MunIPTV/` may live inside it.

## Architecture

### TVapp (Tizen)

- **No framework or bundler.** `index.html` holds all screens; `main.js` toggles the `.active` class between them (`showScreen` / `switchToScreen`).
- **State** is one `state` object in `main.js`. Persistence is `localStorage` (keys `iptv_playlists`, `iptv_active_playlist_id`, `iptv_favorites`).
- **Focus system.** There is no pointer input. `Focus` manages named *zones*, each returning a matrix (rows of elements) via `getMatrix()`. D-pad keys call `Focus.move`, Enter calls `el.click()`. `onEdge` hooks hand focus between zones (e.g. back to `iconRail`). New focusable UI = new zone + `tabindex="-1"` elements.
- **Key handling** uses `keyCode` only (Tizen WebKit); Back is 10009, Channel Up/Down 427/428, Play/Pause 10252 (registered through `tizen.tvinputdevice`).
- **Data flow:** `parseM3U` -> channels tagged with `playlistId`/`playlistName` -> `rebuildCategories` groups by `group-title` *per playlist* -> render functions build DOM.
- **Playback** uses hls.js (`Hls.isSupported()`), falling back to native HLS. Fatal network/media errors are auto-recovered.
- The built-in playlist (`id: 'default'`) always tracks the bundled URL; at least one playlist must remain.

### MunIPTV (MAUI)

- **`PlaylistService` is the single source of truth** (registered as a singleton). It owns playlists, active ids, the merged channel list, `Categories` (`List<CategoryGroup>`, one group per playlist), favorites, and persistence (`Preferences` + System.Text.Json). It raises `Changed` after every mutation; pages subscribe in `OnAppearing`, unsubscribe in `OnDisappearing`, and re-bind in a `Refresh()` method.
- **Initialization** is lazy and idempotent: `EnsureInitializedAsync()` loads storage and fetches all active playlists in parallel. Failures are collected in `StartupErrors` rather than thrown.
- **Fetching:** `asset://<name>` reads a bundled MauiAsset; anything else is HTTP(S) with a 15 s connect timeout and a 90 s download timeout. Parsing runs on `Task.Run`.
- **Navigation:** Shell tabs for top-level pages; `channels` and `player` are pushed routes. Objects are passed with `ShellNavigationQueryParameters` and received via `IQueryAttributable`. Use `PlayerNav.PlayAsync(channel, zapList)` to open the player (the name avoids clashing with `Page.Navigation`).
- **Player** uses `CommunityToolkit.Maui.MediaElement` (ExoPlayer/Media3, HLS included) with custom overlay controls. The zap list is whatever list the user came from (category or favorites).
- **Lists** use `CollectionView` with `SelectionMode="Single"`; handlers reset `SelectedItem = null` after navigating.
- **Android specifics:** cleartext HTTP is allowed in the manifest (many streams/playlists are `http://`); orientation is locked to `SensorLandscape` in `MainActivity`.

## Commands

```bash
# TVapp (run from TVapp/)
cd TVapp
tizen build-web -- .
tizen package -t wgt -s <security-profile> -- .buildResult

# MunIPTV (from repo root; needs .NET 10 SDK + maui-android workload)
dotnet build MunIPTV/MunIPTV.csproj -f net10.0-android
dotnet build MunIPTV/MunIPTV.csproj -f net10.0-android -t:Run
```

There are no automated tests in either project.

## Conventions and gotchas

- The bundled playlist exists twice: `TVapp/playlists/index.country.m3u` and `MunIPTV/Resources/Raw/index.country.m3u`. Update both if you refresh it.
- `TVapp.wgt` is a committed build artifact; do not edit it by hand. `.buildResult/` and `bin/`/`obj/` are gitignored.
- `TVapp/js/main.js` is ES5-style (`var`, no modules) on purpose, to run on old Tizen WebKit. Do not introduce newer syntax there.
- MunIPTV package versions are pinned to what exists on NuGet for .NET 10: `CommunityToolkit.Maui` 16.0.0 and `CommunityToolkit.Maui.MediaElement` 11.0.0, which need `Microsoft.Maui.Controls` >= 10.0.90.
- The MAUI app was written without a .NET SDK available and has not been compiled or run on a device; expect to fix small build issues on first build.
- Feature parity gaps in MunIPTV: no favorite-star badge on channel tiles; no D-pad/Android TV focus handling.
