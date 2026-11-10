namespace MunIPTV.Pages;

public partial class SupportPage : ContentPage
{
    const string DonateUrl = "https://donate.stripe.com/8x2eVef1M8vR58513Ebwk01";

    public SupportPage() => InitializeComponent();

    async void OnOpen(object? sender, EventArgs e)
    {
        try { await Launcher.Default.OpenAsync(DonateUrl); }
        catch { /* no browser available */ }
    }
}
