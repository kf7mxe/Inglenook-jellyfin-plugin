using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Inglenook.Models.OpenLibrary;

/// <summary>
/// Open Library search API response.
/// </summary>
public class OpenLibrarySearchResponse
{
    [JsonPropertyName("numFound")]
    public int NumFound { get; set; }

    [JsonPropertyName("docs")]
    public List<OpenLibraryDoc> Docs { get; set; } = new();
}

/// <summary>
/// A single document (work) from Open Library search results.
/// </summary>
public class OpenLibraryDoc
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author_name")]
    public List<string>? AuthorName { get; set; }

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("publisher")]
    public List<string>? Publisher { get; set; }

    [JsonPropertyName("isbn")]
    public List<string>? Isbn { get; set; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; set; }

    [JsonPropertyName("subject")]
    public List<string>? Subject { get; set; }

    [JsonPropertyName("language")]
    public List<string>? Language { get; set; }

    [JsonPropertyName("number_of_pages_median")]
    public int? NumberOfPagesMedian { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets the cover image URL for this work, or null if no cover is available.
    /// </summary>
    public string? CoverImageUrl => CoverId.HasValue
        ? $"https://covers.openlibrary.org/b/id/{CoverId}-M.jpg"
        : null;

    /// <summary>
    /// Gets the best ISBN (prefers ISBN-13 over ISBN-10).
    /// </summary>
    public string? BestIsbn => Isbn?
        .FirstOrDefault(i => i.Length == 13)
        ?? Isbn?.FirstOrDefault();
}
