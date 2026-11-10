using MunIPTV.Models;
using MunIPTV.Services;

namespace MunIPTV.Pages;

public partial class ChannelsPage : ContentPage, IQueryAttributable
{
    Category? _category;

    public ChannelsPage() => InitializeComponent();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _category = query["Category"] as Category;
        Title = _category is null ? "Channels" : _category.Name;
        List.ItemsSource = _category?.Channels;
    }

    async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Channel ch || _category is null) return;
        List.SelectedItem = null;
        await PlayerNav.PlayAsync(ch, _category.Channels);
    }
}
