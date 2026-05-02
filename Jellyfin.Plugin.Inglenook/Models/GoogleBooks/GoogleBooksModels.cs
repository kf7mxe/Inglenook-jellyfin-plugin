using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Inglenook.Models.GoogleBooks;

/// <summary>
/// Google Books search response.
/// </summary>
public class GoogleBooksSearchResponse
{
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("items")]
    public List<GoogleBooksVolume>? Items { get; set; }
}

/// <summary>
/// Google Books volume entry.
/// </summary>
public class GoogleBooksVolume
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("volumeInfo")]
    public GoogleBooksVolumeInfo? VolumeInfo { get; set; }
}

/// <summary>
/// Volume metadata from Google Books.
/// </summary>
public class GoogleBooksVolumeInfo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("authors")]
    public List<string>? Authors { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("industryIdentifiers")]
    public List<GoogleBooksIdentifier>? IndustryIdentifiers { get; set; }

    [JsonPropertyName("pageCount")]
    public int? PageCount { get; set; }

    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }

    [JsonPropertyName("averageRating")]
    public float? AverageRating { get; set; }

    [JsonPropertyName("imageLinks")]
    public GoogleBooksImageLinks? ImageLinks { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

/// <summary>
/// Industry identifier (ISBN, etc.) from Google Books.
/// </summary>
public class GoogleBooksIdentifier
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;
}

/// <summary>
/// Image links from Google Books.
/// </summary>
public class GoogleBooksImageLinks
{
    [JsonPropertyName("smallThumbnail")]
    public string? SmallThumbnail { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("small")]
    public string? Small { get; set; }

    [JsonPropertyName("medium")]
    public string? Medium { get; set; }

    [JsonPropertyName("large")]
    public string? Large { get; set; }

    [JsonPropertyName("extraLarge")]
    public string? ExtraLarge { get; set; }

    /// <summary>
    /// Gets the best available image URL, preferring larger sizes.
    /// </summary>
    public string? BestImage =>
        ExtraLarge ?? Large ?? Medium ?? Small ?? Thumbnail ?? SmallThumbnail;
}
