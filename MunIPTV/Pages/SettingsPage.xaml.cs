using MunIPTV.Models;
using MunIPTV.Services;

namespace MunIPTV.Pages;

public sealed record PlaylistRow(Playlist Playlist, bool IsActive);

public partial class SettingsPage : ContentPage
{
    readonly PlaylistService _svc;

    public SettingsPage(PlaylistService svc)
    {
        InitializeComponent();
        _svc = svc;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _svc.Changed += Refresh;
        await _svc.EnsureInitializedAsync();
        Refresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _svc.Changed -= Refresh;
    }

    void Refresh()
    {
        Busy.IsVisible = Busy.IsRunning = _svc.IsLoading;
        List.ItemsSource = _svc.Playlists.Select(p => new PlaylistRow(p, _svc.IsActive(p))).ToList();
        AddIptvOrgButton.IsVisible = !_svc.HasIptvOrg;
    }

    static PlaylistRow? RowOf(object? sender) => (sender as BindableObject)?.BindingContext as PlaylistRow;

    static void ReportLoad(string name, string? error)
    {
        if (error is not null) Ui.Toast($"Could not load \"{name}\": {error}");
    }

    async void OnToggled(object? sender, ToggledEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        // Toggled also fires while the template binds its initial value; only act on real changes.
        if (e.Value == _svc.IsActive(row.Playlist)) return;
        ReportLoad(row.Playlist.Name, await _svc.ToggleActiveAsync(row.Playlist));
    }

    async void OnAdd(object? sender, EventArgs e)
    {
        var url = await PromptUrlAsync("Add M3U Playlist", "http://");
        if (url is null) return;
        Ui.Toast("Playlist added");
        ReportLoad(PlaylistService.DeriveName(url), await _svc.AddPlaylistAsync(url));
    }

    async void OnAddIptvOrg(object? sender, EventArgs e)
    {
        Ui.Toast("Playlist added");
        ReportLoad("iptv-org.github.io", await _svc.AddPlaylistAsync(PlaylistService.IptvOrgUrl));
    }

    async void OnEdit(object? sender, EventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var url = await PromptUrlAsync("Edit Playlist", row.Playlist.Url);
        if (url is null) return;
        Ui.Toast("Playlist updated");
        ReportLoad(PlaylistService.DeriveName(url), await _svc.UpdatePlaylistAsync(row.Playlist, url));
    }

    async void OnDelete(object? sender, EventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (_svc.Playlists.Count <= 1) { Ui.Toast("You need at least one playlist"); return; }
        if (!await DisplayAlert("Delete playlist", $"Remove \"{row.Playlist.Name}\"?", "Delete", "Cancel")) return;
        if (_svc.RemovePlaylist(row.Playlist)) Ui.Toast("Playlist removed");
    }

    async Task<string?> PromptUrlAsync(string title, string initial)
    {
        var url = await DisplayPromptAsync(title, "URL of your M3U / M3U8 playlist", "Save", "Cancel",
            initialValue: initial, keyboard: Keyboard.Url);
        url = url?.Trim();
        if (string.IsNullOrEmpty(url)) return null;
        if (url is "http://" or "https://" || !Uri.TryCreate(url, UriKind.Absolute, out var u) ||
            (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
        {
            Ui.Toast("Enter a valid playlist URL first");
            return null;
        }
        return url;
    }
}
