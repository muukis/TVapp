using System.Text.Json;
using MunIPTV.Models;

namespace MunIPTV.Services;

/// <summary>
/// App state: playlists (add/edit/delete, several active at once), the merged channel
/// list, categories grouped per playlist, and favorites. Port of the Tizen app's main.js.
/// </summary>
public sealed class PlaylistService
{
    const string PlaylistsKey = "iptv_playlists";
    const string ActiveKey = "iptv_active_playlists";
    const string FavoritesKey = "iptv_favorites";

    public const string BundledUrl = "asset://index.country.m3u";
    public const string IptvOrgUrl = "https://iptv-org.github.io/iptv/index.country.m3u";

    static readonly Playlist DefaultPlaylist = new() { Id = "default", Name = "IPTV-ORG Countries", Url = BundledUrl };

    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(90);

    List<Channel> _channels = [];
    Task? _init;

    public List<Playlist> Playlists { get; private set; } = [];
    public HashSet<string> ActiveIds { get; private set; } = [];
    public List<Channel> Favorites { get; private set; } = [];
    public List<CategoryGroup> Categories { get; private set; } = [];
    public bool IsLoading { get; private set; }

    /// <summary>Raised on the UI thread whenever channels, playlists or favorites change.</summary>
    public event Action? Changed;

    // ---------------------------------------------------------------- startup

    /// <summary>Loads storage and fetches all active playlists once. Returns load errors, if any.</summary>
    public Task EnsureInitializedAsync() => _init ??= InitializeAsync();

    public List<string> StartupErrors { get; } = [];

    async Task InitializeAsync()
    {
        LoadStorage();
        IsLoading = true;
        Changed?.Invoke();

        var active = Playlists.Where(p => ActiveIds.Contains(p.Id)).ToList();
        var results = await Task.WhenAll(active.Select(async p => (p, error: await FetchAsync(p))));

        foreach (var (p, error) in results.Where(r => r.error.Error is not null))
            StartupErrors.Add($"{p.Name}: {error.Error}");
        _channels = results.SelectMany(r => r.error.Channels).ToList();

        RebuildCategories();
        IsLoading = false;
        Changed?.Invoke();
    }

    void LoadStorage()
    {
        Playlists = ReadJson<List<Playlist>>(PlaylistsKey) is { Count: > 0 } stored ? stored : [Clone(DefaultPlaylist)];

        // The built-in entry always tracks the current bundled URL.
        var builtIn = Playlists.FirstOrDefault(p => p.Id == DefaultPlaylist.Id);
        if (builtIn is not null && builtIn.Url != DefaultPlaylist.Url)
        {
            builtIn.Url = DefaultPlaylist.Url;
            builtIn.Name = DefaultPlaylist.Name;
            SavePlaylists();
        }

        var ids = ReadJson<List<string>>(ActiveKey) ?? [DefaultPlaylist.Id];
        var valid = Playlists.Select(p => p.Id).ToHashSet();
        ActiveIds = ids.Where(valid.Contains).ToHashSet();

        Favorites = ReadJson<List<Channel>>(FavoritesKey) ?? [];
    }

    // ---------------------------------------------------------------- fetching

    static async Task<string> ReadPlaylistTextAsync(Playlist pl)
    {
        if (pl.Url.StartsWith("asset://", StringComparison.Ordinal))
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(pl.Url["asset://".Length..]);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        using var connectCts = new CancellationTokenSource(ConnectTimeout);
        using var response = await Http.GetAsync(pl.Url, HttpCompletionOption.ResponseHeadersRead, connectCts.Token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
        using var downloadCts = new CancellationTokenSource(DownloadTimeout);
        return await response.Content.ReadAsStringAsync(downloadCts.Token);
    }

    static async Task<(List<Channel> Channels, string? Error)> FetchAsync(Playlist pl)
    {
        try
        {
            var text = await ReadPlaylistTextAsync(pl);
            var channels = await Task.Run(() => M3UParser.Parse(text));
            if (channels.Count == 0) return ([], "No channels found");
            foreach (var ch in channels) { ch.PlaylistId = pl.Id; ch.PlaylistName = pl.Name; }
            return (channels, null);
        }
        catch (OperationCanceledException) { return ([], "Timed out"); }
        catch (Exception ex) { return ([], ex.Message); }
    }

    /// <summary>Fetches one playlist and merges it in, replacing anything it contributed before.</summary>
    public async Task<string?> LoadPlaylistAsync(Playlist pl)
    {
        IsLoading = true;
        Changed?.Invoke();
        var (channels, error) = await FetchAsync(pl);
        _channels = _channels.Where(c => c.PlaylistId != pl.Id).Concat(channels).ToList();
        RebuildCategories();
        IsLoading = false;
        Changed?.Invoke();
        return error;
    }

    void RemoveChannelsFor(string playlistId)
    {
        _channels = _channels.Where(c => c.PlaylistId != playlistId).ToList();
        RebuildCategories();
        Changed?.Invoke();
    }

    // Categories are grouped per source playlist, never merged across playlists.
    void RebuildCategories()
    {
        Categories = _channels
            .GroupBy(c => c.PlaylistId)
            .Select(pg => new CategoryGroup(
                pg.First().PlaylistName,
                pg.GroupBy(c => c.Group)
                  .Select(g => new Category
                  {
                      Name = g.Key,
                      PlaylistId = pg.Key,
                      PlaylistName = pg.First().PlaylistName,
                      Channels = g.ToList(),
                  })
                  .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)))
            .ToList();
    }

    // ---------------------------------------------------------------- playlists

    public bool IsActive(Playlist pl) => ActiveIds.Contains(pl.Id);
    public bool HasIptvOrg => Playlists.Any(p => p.Url == IptvOrgUrl);

    /// <summary>Activates or deactivates a playlist. Returns a load error when activation fails.</summary>
    public async Task<string?> ToggleActiveAsync(Playlist pl)
    {
        if (!ActiveIds.Remove(pl.Id))
        {
            ActiveIds.Add(pl.Id);
            SaveActive();
            Changed?.Invoke();
            return await LoadPlaylistAsync(pl);
        }
        SaveActive();
        RemoveChannelsFor(pl.Id);
        return null;
    }

    public async Task<string?> AddPlaylistAsync(string url)
    {
        var pl = new Playlist { Id = "pl_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Name = DeriveName(url), Url = url };
        Playlists.Add(pl);
        ActiveIds.Add(pl.Id);
        SavePlaylists();
        SaveActive();
        Changed?.Invoke();
        return await LoadPlaylistAsync(pl);
    }

    public async Task<string?> UpdatePlaylistAsync(Playlist pl, string url)
    {
        pl.Url = url;
        pl.Name = DeriveName(url);
        SavePlaylists();
        Changed?.Invoke();
        return ActiveIds.Contains(pl.Id) ? await LoadPlaylistAsync(pl) : null;
    }

    /// <summary>Removes a playlist and its favorites. The last remaining playlist cannot be removed.</summary>
    public bool RemovePlaylist(Playlist pl)
    {
        if (Playlists.Count <= 1) return false;
        Playlists.RemoveAll(p => p.Id == pl.Id);
        ActiveIds.Remove(pl.Id);
        if (Favorites.RemoveAll(f => f.PlaylistId == pl.Id) > 0) SaveFavorites();
        SavePlaylists();
        SaveActive();
        RemoveChannelsFor(pl.Id);
        return true;
    }

    public static string DeriveName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u))
        {
            var last = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return u.Host + (last is null ? "" : " / " + last);
        }
        return url.Length > 40 ? url[..40] + "…" : url;
    }

    // ---------------------------------------------------------------- favorites

    // Favorites persist regardless of which playlists are active, but are only shown
    // while their source playlist is active.
    public List<Channel> VisibleFavorites =>
        Favorites.Where(f => string.IsNullOrEmpty(f.PlaylistId) || ActiveIds.Contains(f.PlaylistId)).ToList();

    public bool IsFavorite(Channel ch) => Favorites.Any(f => f.Url == ch.Url);

    /// <summary>Returns true when the channel was added, false when removed.</summary>
    public bool ToggleFavorite(Channel ch)
    {
        var existing = Favorites.FirstOrDefault(f => f.Url == ch.Url);
        var added = existing is null;
        if (added) Favorites.Add(ch); else Favorites.Remove(existing!);
        SaveFavorites();
        Changed?.Invoke();
        return added;
    }

    /// <summary>The channel shown in the Home hero: first favorite, else the first channel.</summary>
    public Channel? Featured => VisibleFavorites.FirstOrDefault() ?? Categories.FirstOrDefault()?.FirstOrDefault()?.Channels.FirstOrDefault();

    // ---------------------------------------------------------------- storage

    static T? ReadJson<T>(string key)
    {
        try
        {
            var raw = Preferences.Default.Get(key, "");
            return raw.Length == 0 ? default : JsonSerializer.Deserialize<T>(raw);
        }
        catch { return default; }
    }

    static void WriteJson<T>(string key, T value)
    {
        try { Preferences.Default.Set(key, JsonSerializer.Serialize(value)); } catch { /* non-fatal */ }
    }

    static Playlist Clone(Playlist p) => new() { Id = p.Id, Name = p.Name, Url = p.Url };
    void SavePlaylists() => WriteJson(PlaylistsKey, Playlists);
    void SaveActive() => WriteJson(ActiveKey, ActiveIds.ToList());
    void SaveFavorites() => WriteJson(FavoritesKey, Favorites);
}
