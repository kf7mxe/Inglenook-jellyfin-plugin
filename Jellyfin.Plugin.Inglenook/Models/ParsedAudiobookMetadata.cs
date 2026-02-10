using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Inglenook.Models;

/// <summary>
/// Unified model for audiobook metadata parsed from any source.
/// </summary>
public class ParsedAudiobookMetadata
{
    /// <summary>Gets or sets the source file path this metadata was parsed from.</summary>
    public string? SourceFile { get; set; }

    /// <summary>Gets or sets the source type identifier (e.g., "cue", "opf", "json").</summary>
    public string? SourceType { get; set; }

    /// <summary>Gets or sets the book title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the sort title.</summary>
    public string? SortTitle { get; set; }

    /// <summary>Gets or sets the original title.</summary>
    public string? OriginalTitle { get; set; }

    /// <summary>Gets or sets the subtitle.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Gets or sets the book description or synopsis.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the list of authors.</summary>
    public List<string> Authors { get; set; } = new();

    /// <summary>Gets or sets the list of narrators.</summary>
    public List<string> Narrators { get; set; } = new();

    /// <summary>Gets or sets the publisher name.</summary>
    public string? Publisher { get; set; }

    /// <summary>Gets or sets the publication date.</summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>Gets or sets the publication year.</summary>
    public int? Year { get; set; }

    /// <summary>Gets or sets the list of genres.</summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>Gets or sets the list of tags.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Gets or sets the language code.</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the community rating (0-10).</summary>
    public float? CommunityRating { get; set; }

    /// <summary>Gets or sets the critic rating (0-100).</summary>
    public float? CriticRating { get; set; }

    /// <summary>Gets or sets a value indicating whether the audiobook is abridged.</summary>
    public bool? Abridged { get; set; }

    /// <summary>Gets or sets the series name.</summary>
    public string? SeriesName { get; set; }

    /// <summary>Gets or sets the position within the series.</summary>
    public float? SeriesIndex { get; set; }

    /// <summary>Gets or sets the ISBN-10.</summary>
    public string? Isbn { get; set; }

    /// <summary>Gets or sets the ISBN-13.</summary>
    public string? Isbn13 { get; set; }

    /// <summary>Gets or sets the ASIN.</summary>
    public string? Asin { get; set; }

    /// <summary>Gets or sets the Audible ASIN.</summary>
    public string? AudibleAsin { get; set; }

    /// <summary>Gets or sets the Goodreads ID.</summary>
    public string? GoodreadsId { get; set; }

    /// <summary>Gets or sets the Google Books ID.</summary>
    public string? GoogleBooksId { get; set; }

    /// <summary>Gets or sets the Open Library ID.</summary>
    public string? OpenLibraryId { get; set; }

    /// <summary>Gets or sets additional provider IDs.</summary>
    public Dictionary<string, string> ProviderIds { get; set; } = new();

    /// <summary>Gets or sets the chapter list.</summary>
    public List<ChapterInfo> Chapters { get; set; } = new();

    /// <summary>Gets or sets the total duration in ticks.</summary>
    public long? DurationTicks { get; set; }

    /// <summary>Gets or sets the path to a cover image.</summary>
    public string? CoverImagePath { get; set; }

    /// <summary>Gets a value indicating whether this metadata contains chapter information.</summary>
    public bool HasChapters => Chapters.Count > 0;

    /// <summary>Gets a value indicating whether this metadata has any meaningful content.</summary>
    public bool HasContent =>
        !string.IsNullOrEmpty(Title) ||
        Authors.Count > 0 ||
        Narrators.Count > 0 ||
        HasChapters ||
        !string.IsNullOrEmpty(Description);
}
