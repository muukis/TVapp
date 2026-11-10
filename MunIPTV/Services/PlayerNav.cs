using MunIPTV.Models;

namespace MunIPTV.Services;

public static class PlayerNav
{
    /// <summary>Opens the player for a channel; <paramref name="zapList"/> is what Prev/Next cycle through.</summary>
    public static Task PlayAsync(Channel channel, IList<Channel> zapList) =>
        Shell.Current.GoToAsync("player", new ShellNavigationQueryParameters
        {
            ["Channel"] = channel,
            ["ZapList"] = zapList,
        });
}
