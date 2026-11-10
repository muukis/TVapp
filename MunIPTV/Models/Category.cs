namespace MunIPTV.Models;

public class Category
{
    public string Name { get; set; } = "";
    public string PlaylistId { get; set; } = "";
    public string PlaylistName { get; set; } = "";
    public List<Channel> Channels { get; set; } = [];

    public int Count => Channels.Count;
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}

/// <summary>Categories of one playlist; the unit of a grouped CollectionView.</summary>
public class CategoryGroup : List<Category>
{
    public string PlaylistName { get; }

    public CategoryGroup(string playlistName, IEnumerable<Category> categories) : base(categories)
        => PlaylistName = string.IsNullOrWhiteSpace(playlistName) ? "Playlist" : playlistName;
}
