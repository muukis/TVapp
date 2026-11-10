using MunIPTV.Pages;

namespace MunIPTV;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("channels", typeof(ChannelsPage));
        Routing.RegisterRoute("player", typeof(PlayerPage));
    }
}
