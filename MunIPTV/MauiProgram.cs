using CommunityToolkit.Maui;
using MunIPTV.Pages;
using MunIPTV.Services;

namespace MunIPTV;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement();

        builder.Services.AddSingleton<PlaylistService>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<ChannelsPage>();
        builder.Services.AddTransient<SupportPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<PlayerPage>();

        return builder.Build();
    }
}
