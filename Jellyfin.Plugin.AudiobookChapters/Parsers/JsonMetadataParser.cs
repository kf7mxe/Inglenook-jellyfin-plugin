using System.Text.Json;
using Jellyfin.Plugin.AudiobookChapters.Models;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AudiobookChapters.Parsers;

/// <summary>
/// Parser for JSON metadata files supporting multiple formats.
/// </summary>
public class JsonMetadataParser : IMetadataParser
{
    /// <inheritdoc />
    public string Name => "JSON Metadata";

    /// <inheritdoc />
    public int Priority => 90;

    /// <inheritdoc />
    public string[] SupportedExtensions => new[] { ".json" };

    /// <inheritdoc />
    public string[] FilePatterns => new[]
    {
        "metadata.json", "audiobook.json", "book.json",
        "chapters.json", "abs.json", "info.json"
    };

    /// <inheritdoc />
    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(filePath);
        return FilePatterns.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ParsedAudiobookMetadata?> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return ParseContent(content, filePath);
    }

    /// <inheritdoc />
    public ParsedAudiobookMetadata? ParseContent(string content, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Check if it's a chapters-only array
            if (root.ValueKind == JsonValueKind.Array)
            {
                return ParseChaptersArray(root, sourcePath);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Check for Audiobookshelf (abs.json) format
            if (root.TryGetProperty("libraryItem", out var libraryItem)
                || root.TryGetProperty("mediaMetadata", out _))
            {
                return ParseAudiobookshelfFormat(root, sourcePath);
            }

            // Generic format
            return ParseGenericFormat(root, sourcePath);
        }
    }

    private static ParsedAudiobookMetadata? ParseChaptersArray(JsonElement array, string? sourcePath)
    {
        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "json"
        };

        foreach (var item in array.EnumerateArray())
        {
            var chapter = ParseChapterElement(item);
            if (chapter is not null)
            {
                metadata.Chapters.Add(chapter);
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static ParsedAudiobookMetadata? ParseAudiobookshelfFormat(JsonElement root, string? sourcePath)
    {
        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "json"
        };

        // Navigate to the metadata object
        JsonElement metaElement;
        if (root.TryGetProperty("libraryItem", out var libraryItem))
        {
            if (libraryItem.TryGetProperty("media", out var media)
                && media.TryGetProperty("metadata", out var mediaMeta))
            {
                metaElement = mediaMeta;
            }
            else
            {
                return null;
            }

            // Chapters from media
            if (libraryItem.TryGetProperty("media", out var mediaForChapters)
                && mediaForChapters.TryGetProperty("chapters", out var chaptersElement)
                && chaptersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var ch in chaptersElement.EnumerateArray())
                {
                    var chapter = ParseChapterElement(ch);
                    if (chapter is not null)
                    {
                        metadata.Chapters.Add(chapter);
                    }
                }
            }
        }
        else if (root.TryGetProperty("mediaMetadata", out var mediaMeta2))
        {
            metaElement = mediaMeta2;

            if (root.TryGetProperty("chapters", out var chaptersElement2)
                && chaptersElement2.ValueKind == JsonValueKind.Array)
            {
                foreach (var ch in chaptersElement2.EnumerateArray())
                {
                    var chapter = ParseChapterElement(ch);
                    if (chapter is not null)
                    {
                        metadata.Chapters.Add(chapter);
                    }
                }
            }
        }
        else
        {
            return null;
        }

        metadata.Title = GetStringProperty(metaElement, "title");
        metadata.Subtitle = GetStringProperty(metaElement, "subtitle");
        metadata.Description = GetStringProperty(metaElement, "description");
        metadata.Publisher = GetStringProperty(metaElement, "publisher");
        metadata.Language = GetStringProperty(metaElement, "language");
        metadata.Asin = GetStringProperty(metaElement, "asin");
        metadata.Isbn = GetStringProperty(metaElement, "isbn");

        if (metaElement.TryGetProperty("publishedYear", out var pubYear))
        {
            var yearStr = pubYear.ValueKind == JsonValueKind.Number
                ? pubYear.GetInt32().ToString()
                : pubYear.GetString();
            if (int.TryParse(yearStr, out var year))
            {
                metadata.Year = year;
            }
        }

        if (metaElement.TryGetProperty("abridged", out var abridged) && abridged.ValueKind == JsonValueKind.True)
        {
            metadata.Abridged = true;
        }

        // Authors as objects with name property
        if (metaElement.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
        {
            foreach (var author in authors.EnumerateArray())
            {
                string? name = author.ValueKind == JsonValueKind.Object
                    ? GetStringProperty(author, "name")
                    : author.GetString();
                if (!string.IsNullOrEmpty(name) && !metadata.Authors.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Authors.Add(name);
                }
            }
        }

        // Narrators as objects or strings
        if (metaElement.TryGetProperty("narrators", out var narrators) && narrators.ValueKind == JsonValueKind.Array)
        {
            foreach (var narrator in narrators.EnumerateArray())
            {
                string? name = narrator.ValueKind == JsonValueKind.Object
                    ? GetStringProperty(narrator, "name")
                    : narrator.GetString();
                if (!string.IsNullOrEmpty(name) && !metadata.Narrators.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Narrators.Add(name);
                }
            }
        }

        // Series as array
        if (metaElement.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in series.EnumerateArray())
            {
                metadata.SeriesName ??= GetStringProperty(s, "name");
                var seq = GetStringProperty(s, "sequence");
                if (!string.IsNullOrEmpty(seq) && float.TryParse(seq, out var idx))
                {
                    metadata.SeriesIndex ??= idx;
                }
            }
        }

        // Genres
        if (metaElement.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in genres.EnumerateArray())
            {
                var genre = g.GetString();
                if (!string.IsNullOrEmpty(genre) && !metadata.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Genres.Add(genre);
                }
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static ParsedAudiobookMetadata? ParseGenericFormat(JsonElement root, string? sourcePath)
    {
        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "json"
        };

        // Title
        metadata.Title = GetStringProperty(root, "title")
            ?? GetStringProperty(root, "name")
            ?? GetStringProperty(root, "bookTitle");

        metadata.SortTitle = GetStringProperty(root, "sortTitle") ?? GetStringProperty(root, "titleSort");
        metadata.Description = GetStringProperty(root, "description")
            ?? GetStringProperty(root, "summary")
            ?? GetStringProperty(root, "synopsis");
        metadata.Publisher = GetStringProperty(root, "publisher");
        metadata.Language = GetStringProperty(root, "language");

        // Authors
        ParseStringOrArray(root, metadata.Authors, "author", "authors", "writer", "writers");

        // Narrators
        ParseStringOrArray(root, metadata.Narrators, "narrator", "narrators", "reader", "readers");

        // Year
        metadata.Year = GetIntProperty(root, "year") ?? GetIntProperty(root, "publishedYear");

        // Date
        var dateStr = GetStringProperty(root, "date")
            ?? GetStringProperty(root, "publishedDate")
            ?? GetStringProperty(root, "releaseDate");
        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
        {
            metadata.PublishedDate = date;
            metadata.Year ??= date.Year;
        }

        // Identifiers
        metadata.Isbn = GetStringProperty(root, "isbn");
        metadata.Isbn13 = GetStringProperty(root, "isbn13");
        metadata.Asin = GetStringProperty(root, "asin");
        metadata.AudibleAsin = GetStringProperty(root, "audibleAsin");
        metadata.GoodreadsId = GetStringProperty(root, "goodreadsId") ?? GetStringProperty(root, "goodreads");
        metadata.GoogleBooksId = GetStringProperty(root, "googleBooksId");

        // Rating
        metadata.CommunityRating = GetFloatProperty(root, "rating")
            ?? GetFloatProperty(root, "communityRating");

        // Abridged
        if (root.TryGetProperty("abridged", out var abridged))
        {
            metadata.Abridged = abridged.ValueKind == JsonValueKind.True;
        }

        // Series
        if (root.TryGetProperty("series", out var series))
        {
            if (series.ValueKind == JsonValueKind.Object)
            {
                metadata.SeriesName = GetStringProperty(series, "name") ?? GetStringProperty(series, "title");
                metadata.SeriesIndex = GetFloatProperty(series, "position")
                    ?? GetFloatProperty(series, "index")
                    ?? GetFloatProperty(series, "number");
            }
            else if (series.ValueKind == JsonValueKind.String)
            {
                metadata.SeriesName = series.GetString();
            }
        }

        metadata.SeriesIndex ??= GetFloatProperty(root, "seriesIndex")
            ?? GetFloatProperty(root, "seriesPosition");

        // Genres
        if (root.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in genres.EnumerateArray())
            {
                var genre = g.GetString();
                if (!string.IsNullOrEmpty(genre) && !metadata.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Genres.Add(genre);
                }
            }
        }

        // Tags
        if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tags.EnumerateArray())
            {
                var tag = t.GetString();
                if (!string.IsNullOrEmpty(tag) && !metadata.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Tags.Add(tag);
                }
            }
        }

        // Chapters
        if (root.TryGetProperty("chapters", out var chapters) && chapters.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in chapters.EnumerateArray())
            {
                var chapter = ParseChapterElement(ch);
                if (chapter is not null)
                {
                    metadata.Chapters.Add(chapter);
                }
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static ChapterInfo? ParseChapterElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = GetStringProperty(element, "title")
            ?? GetStringProperty(element, "name")
            ?? GetStringProperty(element, "chapterTitle");

        var startSeconds = GetFloatProperty(element, "start")
            ?? GetFloatProperty(element, "startTime")
            ?? GetFloatProperty(element, "startOffset");

        // Some formats use milliseconds
        var startMs = GetFloatProperty(element, "startMs")
            ?? GetFloatProperty(element, "startTimeMs");

        long startTicks;
        if (startSeconds.HasValue)
        {
            startTicks = (long)(startSeconds.Value * TimeSpan.TicksPerSecond);
        }
        else if (startMs.HasValue)
        {
            startTicks = (long)(startMs.Value * TimeSpan.TicksPerMillisecond);
        }
        else
        {
            // Try ticks directly
            if (element.TryGetProperty("startPositionTicks", out var ticksProp)
                && ticksProp.TryGetInt64(out var ticks))
            {
                startTicks = ticks;
            }
            else
            {
                return null;
            }
        }

        return new ChapterInfo
        {
            Name = name ?? $"Chapter {startTicks}",
            StartPositionTicks = startTicks
        };
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
            {
                return value;
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static float? GetFloatProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return (float)prop.GetDouble();
            }

            if (prop.ValueKind == JsonValueKind.String && float.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static void ParseStringOrArray(JsonElement element, List<string> target, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (!element.TryGetProperty(propName, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrEmpty(value) && !target.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    target.Add(value);
                }
            }
            else if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    string? value = item.ValueKind == JsonValueKind.Object
                        ? GetStringProperty(item, "name")
                        : item.GetString();
                    if (!string.IsNullOrEmpty(value) && !target.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        target.Add(value);
                    }
                }
            }
        }
    }
}
