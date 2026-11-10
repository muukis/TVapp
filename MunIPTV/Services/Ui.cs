
namespace MunIPTV.Services;

public static class Ui
{
    public static async void Toast(string message)
    {
        try { await CommunityToolkit.Maui.Alerts.Toast.Make(message).Show(); }
        catch { /* toast is best-effort */ }
    }
}
