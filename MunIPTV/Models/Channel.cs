using System.Text.Json.Serialization;

namespace MunIPTV.Models;

public class Channel
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Logo { get; set; } = "";
    public string Group { get; set; } = "Other";
    public string TvgId { get; set; } = "";
    public string TvgCountry { get; set; } = "";
    public string PlaylistId { get; set; } = "";
    public string PlaylistName { get; set; } = "";

    [JsonIgnore] public bool HasLogo => !string.IsNullOrWhiteSpace(Logo);
    [JsonIgnore] public bool NoLogo => !HasLogo;
    [JsonIgnore] public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}
