using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using MunIPTV.Models;
using MunIPTV.Services;

namespace MunIPTV.Pages;

public partial class PlayerPage : ContentPage, IQueryAttributable
{
    static readonly TimeSpan OverlayHide = TimeSpan.FromSeconds(5);

    readonly PlaylistService _svc;
    IList<Channel> _zapList = [];
    Channel? _current;
    IDispatcherTimer? _hideTimer;

    public PlayerPage(PlaylistService svc)
    {
        InitializeComponent();
        _svc = svc;
        Unloaded += (_, _) => Video.Handler?.DisconnectHandler();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _zapList = query.TryGetValue("ZapList", out var z) && z is IList<Channel> list ? list : [];
        if (query.TryGetValue("Channel", out var c) && c is Channel ch) Play(ch);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true;
        ShowOverlay();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.Current.KeepScreenOn = false;
        _hideTimer?.Stop();
        Video.Stop();
    }

    void Play(Channel ch)
    {
        _current = ch;
        NameLabel.Text = ch.Name;
        GroupLabel.Text = ch.Group;
        UpdateFavoriteButton();
        Spinner.IsVisible = Spinner.IsRunning = true;
        Video.Source = MediaSource.FromUri(ch.Url);
        Video.Play();
        ShowOverlay();
    }

    void Zap(int delta)
    {
        if (_zapList.Count == 0 || _current is null) return;
        var idx = _zapList.ToList().FindIndex(c => c.Url == _current.Url);
        if (idx < 0) idx = 0;
        Play(_zapList[(idx + delta + _zapList.Count) % _zapList.Count]);
    }

    void OnPrev(object? sender, EventArgs e) => Zap(-1);
    void OnNext(object? sender, EventArgs e) => Zap(1);

    void OnPlayPause(object? sender, EventArgs e)
    {
        if (Video.CurrentState == MediaElementState.Playing) Video.Pause(); else Video.Play();
        ShowOverlay();
    }

    void OnFavorite(object? sender, EventArgs e)
    {
        if (_current is null) return;
        Ui.Toast(_svc.ToggleFavorite(_current) ? "Added to Favorites" : "Removed from Favorites");
        UpdateFavoriteButton();
        ShowOverlay();
    }

    void UpdateFavoriteButton() =>
        FavoriteButton.Text = _current is not null && _svc.IsFavorite(_current) ? "★ Favorite" : "☆ Favorite";

    void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        Spinner.IsVisible = Spinner.IsRunning =
            e.NewState is MediaElementState.Opening or MediaElementState.Buffering;
        PlayPauseButton.Text = e.NewState == MediaElementState.Playing ? "⏸ Pause" : "▶ Play";
    }

    void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        Spinner.IsVisible = Spinner.IsRunning = false;
        Ui.Toast("This channel could not be played");
    }

    void OnOverlayTapped(object? sender, TappedEventArgs e)
    {
        if (Overlay.Children.Count > 0 && Overlay.Children[0] is View top && top.IsVisible) HideOverlay();
        else ShowOverlay();
    }

    void ShowOverlay()
    {
        foreach (var child in Overlay.Children.OfType<View>()) child.IsVisible = true;
        _hideTimer ??= Dispatcher.CreateTimer();
        _hideTimer.Interval = OverlayHide;
        _hideTimer.IsRepeating = false;
        _hideTimer.Tick -= OnHideTick;
        _hideTimer.Tick += OnHideTick;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    void OnHideTick(object? sender, EventArgs e) => HideOverlay();

    void HideOverlay()
    {
        _hideTimer?.Stop();
        foreach (var child in Overlay.Children.OfType<View>()) child.IsVisible = false;
    }
}
