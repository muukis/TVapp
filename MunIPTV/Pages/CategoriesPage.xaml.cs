using MunIPTV.Models;
using MunIPTV.Services;

namespace MunIPTV.Pages;

public partial class CategoriesPage : ContentPage
{
    readonly PlaylistService _svc;

    public CategoriesPage(PlaylistService svc)
    {
        InitializeComponent();
        _svc = svc;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _svc.Changed += Refresh;
        Refresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _svc.Changed -= Refresh;
    }

    void Refresh() => List.ItemsSource = _svc.Categories;

    async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Category cat) return;
        List.SelectedItem = null;
        await Shell.Current.GoToAsync("channels", new ShellNavigationQueryParameters { ["Category"] = cat });
    }
}
