using MunIPTV.Models;
using MunIPTV.Services;

namespace MunIPTV.Pages;

public partial class HomePage : ContentPage
{
    readonly PlaylistService _svc;
    bool _shownStartupErrors;

    public HomePage(PlaylistService svc)
    {
        InitializeComponent();
        _svc = svc;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _svc.Changed += Refresh;
        Refresh();

        await _svc.EnsureInitializedAsync();
        if (!_shownStartupErrors && _svc.StartupErrors.Count > 0)
        {
            _shownStartupErrors = true;
            Ui.Toast("Could not load " + string.Join("; ", _svc.StartupErrors));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _svc.Changed -= Refresh;
    }

    void Refresh()
    {
        Busy.IsVisible = Busy.IsRunning = _svc.IsLoading;
        FavoritesList.ItemsSource = _svc.VisibleFavorites;

        var featured = _svc.Featured;
        HeroTitle.Text = featured?.Name ?? "MunIPTV";
        HeroSubtitle.Text = featured is null ? "Add a playlist to get started" : featured.Group;
        HeroImage.Source = featured?.HasLogo == true ? featured.Logo : null;
    }

    async void OnFavoriteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Channel ch) return;
        FavoritesList.SelectedItem = null;
        await PlayerNav.PlayAsync(ch, _svc.VisibleFavorites);
    }
}
