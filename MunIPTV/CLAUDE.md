# CLAUDE.md — MunIPTV (.NET MAUI, Android only)

Android port of the Tizen TV app in `../TVapp/`. See the root `CLAUDE.md` for how the two apps relate. Behavior should stay in step with `../TVapp/js/main.js` (playlists, per-playlist categories, favorites).

## Build and run

Needs the .NET 10 SDK, the `maui-android` workload and the Android SDK (API 36). Run from the repo root:

```bash
dotnet build MunIPTV/MunIPTV.csproj -f net10.0-android
dotnet build MunIPTV/MunIPTV.csproj -f net10.0-android -t:Run          # deploy to device/emulator
dotnet publish MunIPTV/MunIPTV.csproj -f net10.0-android -c Release
```

No tests exist. The project has never been compiled (it was written without an SDK); fix small build errors as they appear.

## Project settings (`MunIPTV.csproj`)

- `TargetFrameworks` is only `net10.0-android`. Do not add iOS/Mac/Windows targets or `Platforms/` folders for them.
- Min Android 24. Application id `com.muukis.muniptv`.
- Packages: `CommunityToolkit.Maui` 16.0.0 (toast, builder extension) and `CommunityToolkit.Maui.MediaElement` 11.0.0 (ExoPlayer/Media3). Both need `Microsoft.Maui.Controls` >= 10.0.90, so the installed MAUI workload must be new enough.
- `Resources/Raw/**` is a `MauiAsset` with `LogicalName` set to the file name, so `FileSystem.OpenAppPackageFileAsync("index.country.m3u")` finds it. `Resources/Images/*` are `MauiImage` (lowercase names, no dashes).

## Layout

```
MauiProgram.cs        Builder: UseMauiCommunityToolkit + UseMauiCommunityToolkitMediaElement; DI registrations
App.xaml(.cs)         Dark theme (UserAppTheme = Dark), color/style resources, CreateWindow -> AppShell
AppShell.xaml(.cs)    TabBar (Home, Categories, Support, Settings); registers routes "channels", "player"
Models/               Playlist, Channel, Category, CategoryGroup (List<Category> with PlaylistName)
Services/
  PlaylistService.cs    All app state + persistence + fetching (singleton)
  M3UParser.cs          #EXTINF parser (tvg-logo, group-title, tvg-id, tvg-country)
  PlayerNav.cs          PlayerNav.PlayAsync(channel, zapList) -> Shell route "player"
  Ui.cs                 Ui.Toast(message)
Pages/                HomePage, CategoriesPage, ChannelsPage, SettingsPage, SupportPage, PlayerPage (each .xaml + .xaml.cs)
Platforms/Android/    AndroidManifest.xml, MainActivity.cs, MainApplication.cs
Resources/            AppIcon/, Splash/, Images/donate_qr.png, Raw/index.country.m3u
```

## Architecture

**State.** `PlaylistService` is registered as a singleton and is the only owner of playlists, active playlist ids, the merged channel list, `Categories` and favorites. It persists via `Preferences` (JSON through System.Text.Json; keys `iptv_playlists`, `iptv_active_playlists`, `iptv_favorites`). Mutating methods raise `Changed`. Pages subscribe in `OnAppearing`, unsubscribe in `OnDisappearing`, and rebind in a `Refresh()` method. Do not keep copies of its lists in pages.

**Startup.** `EnsureInitializedAsync()` is lazy and idempotent (cached `Task`). It loads storage, then fetches every active playlist in parallel. Failures go to `StartupErrors`; `HomePage` shows them once as a toast.

**Fetching.** `asset://name` reads a bundled asset; otherwise HTTP(S) with a 15 s connect timeout (`ResponseHeadersRead`) and a 90 s body timeout. Parsing runs in `Task.Run`. Each playlist is fetched independently, so one bad URL does not blank the others. Loaders return an error string instead of throwing.

**Playlists.** The built-in playlist has id `default` and URL `asset://index.country.m3u`; its URL is reset to that on every load. The last remaining playlist cannot be deleted. Deleting a playlist also deletes its favorites. Deactivating one only hides its favorites.

**Categories.** Grouped by playlist first, then by `group-title`, sorted by name within a playlist. `CategoriesPage` shows them in a grouped `CollectionView` (header = playlist name).

**Navigation.** Top-level pages are Shell tabs created through DI (`ContentTemplate="{DataTemplate pages:HomePage}"`). `channels` and `player` are pushed routes; data goes through `ShellNavigationQueryParameters` and is received by `IQueryAttributable.ApplyQueryAttributes`. The helper is named `PlayerNav` because `Navigation` clashes with `Page.Navigation`.

**Lists.** `CollectionView` with `SelectionMode="Single"`. The handler navigates and then sets `SelectedItem = null` so the same item can be tapped again. Item templates set `x:DataType` for compiled bindings.

**Settings page.** Rows are `PlaylistRow(Playlist, IsActive)` records, rebuilt on every `Changed`. The `Switch.Toggled` handler ignores events where the value already matches `PlaylistService.IsActive`, because `Toggled` also fires during binding. Add/edit use `DisplayPromptAsync` with the URL keyboard; delete uses `DisplayAlert`.

**Player.** `PlayerPage` hosts a `toolkit:MediaElement` (no built-in controls) under a transparent tap layer that toggles the overlay (channel name, favorite, prev/play-pause/next). The overlay auto-hides after 5 s via `IDispatcherTimer`. Prev/Next cycle through the zap list passed in (the category's channels or the visible favorites). `OnDisappearing` stops playback; `Unloaded` disconnects the handler. `KeepScreenOn` is on while the page is shown.

## Android specifics

- `AndroidManifest.xml` sets `usesCleartextTraffic="true"` because many playlists, logos and streams are `http://`. It also declares INTERNET, ACCESS_NETWORK_STATE, WAKE_LOCK and the media-playback foreground-service permissions.
- `MainActivity` is locked to `ScreenOrientation.SensorLandscape`. Layouts should be checked in landscape (the channel grids use `Span="2"`).

## Gotchas

- Updating the bundled playlist means updating two copies: `Resources/Raw/index.country.m3u` here and `../TVapp/playlists/index.country.m3u`.
- Keep Tizen-only files out of this folder, and MAUI files out of `../TVapp/` (the Tizen build packages that whole folder).
- Known gaps vs the Tizen app: no favorite-star badge on channel tiles; no D-pad focus handling for Android TV.
- Add a page by creating the XAML/code-behind pair, registering it with `AddTransient` in `MauiProgram.cs`, and either adding a `ShellContent` in `AppShell.xaml` (tab) or `Routing.RegisterRoute` in `AppShell.xaml.cs` (pushed route).
