using System.Text.RegularExpressions;
using MunIPTV.Models;

namespace MunIPTV.Services;

public static partial class M3UParser
{
    [GeneratedRegex("([a-zA-Z0-9-]+)=\"([^\"]*)\"")]
    private static partial Regex AttrRegex();

    public static List<Channel> Parse(string text)
    {
        var channels = new List<Channel>();
        Channel? pending = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTINF", StringComparison.Ordinal))
            {
                var attrs = new Dictionary<string, string>();
                foreach (Match m in AttrRegex().Matches(line))
                    attrs[m.Groups[1].Value.ToLowerInvariant()] = m.Groups[2].Value;

                // The display name follows the first comma after the last attribute
                // (attribute values may themselves contain commas).
                var searchFrom = Math.Max(0, line.LastIndexOf('"'));
                var comma = line.IndexOf(',', searchFrom);
                var name = comma >= 0 ? line[(comma + 1)..].Trim() : "";

                pending = new Channel
                {
                    Name = name.Length > 0 ? name : "Unnamed Channel",
                    Logo = attrs.GetValueOrDefault("tvg-logo", ""),
                    Group = attrs.GetValueOrDefault("group-title") is { Length: > 0 } g ? g : "Other",
                    TvgId = attrs.GetValueOrDefault("tvg-id", ""),
                    TvgCountry = attrs.GetValueOrDefault("tvg-country", ""),
                };
            }
            else if (!line.StartsWith('#') && pending is not null)
            {
                pending.Url = line;
                channels.Add(pending);
                pending = null;
            }
        }
        return channels;
    }
}
