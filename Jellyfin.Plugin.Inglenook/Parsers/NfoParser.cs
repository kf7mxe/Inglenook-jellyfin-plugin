using System.Xml.Linq;
using Jellyfin.Plugin.Inglenook.Models;

namespace Jellyfin.Plugin.Inglenook.Parsers;

/// <summary>
/// Parser for NFO (Kodi/XBMC) metadata files.
/// </summary>
public class NfoParser : IMetadataParser
{
    private static readonly HashSet<string> SupportedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "audiobook", "book", "album", "movie"
    };

    /// <inheritdoc />
    public string Name => "NFO (Kodi/XBMC)";

    /// <inheritdoc />
    public int Priority => 80;

    /// <inheritdoc />
    public string[] SupportedExtensions => new[] { ".nfo" };

    /// <inheritdoc />
    public string[] FilePatterns => Array.Empty<string>();

    /// <inheritdoc />
    public bool CanParse(string filePath)
    {
        return SupportedExtensions.Contains(
            Path.GetExtension(filePath),
            StringComparer.OrdinalIgnoreCase);
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

        // NFO files sometimes have non-XML content before the XML
        var xmlStart = content.IndexOf('<');
        if (xmlStart < 0)
        {
            return null;
        }

        if (xmlStart > 0)
        {
            content = content[xmlStart..];
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch
        {
            return null;
        }

        var root = doc.Root;
        if (root is null || !SupportedRoots.Contains(root.Name.LocalName))
        {
            return null;
        }

        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "nfo"
        };

        // Title
        metadata.Title = GetElementValue(root, "title");
        metadata.OriginalTitle = GetElementValue(root, "originaltitle");
        metadata.SortTitle = GetElementValue(root, "sorttitle");

        // Description
        metadata.Description = GetElementValue(root, "plot")
            ?? GetElementValue(root, "outline")
            ?? GetElementValue(root, "description");

        // Authors
        AddIfNotEmpty(metadata.Authors, GetElementValue(root, "author"));
        AddIfNotEmpty(metadata.Authors, GetElementValue(root, "artist"));
        AddIfNotEmpty(metadata.Authors, GetElementValue(root, "writer"));

        // Narrators
        AddIfNotEmpty(metadata.Narrators, GetElementValue(root, "narrator"));
        AddIfNotEmpty(metadata.Narrators, GetElementValue(root, "reader"));
        AddIfNotEmpty(metadata.Narrators, GetElementValue(root, "performer"));

        // Actors with narrator role
        foreach (var actor in root.Elements("actor"))
        {
            var role = GetElementValue(actor, "role");
            var name = GetElementValue(actor, "name");
            if (!string.IsNullOrEmpty(name)
                && string.Equals(role, "narrator", StringComparison.OrdinalIgnoreCase)
                && !metadata.Narrators.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Narrators.Add(name);
            }
        }

        // Publisher
        metadata.Publisher = GetElementValue(root, "publisher")
            ?? GetElementValue(root, "studio")
            ?? GetElementValue(root, "label");

        // Year and dates
        var yearStr = GetElementValue(root, "year");
        if (int.TryParse(yearStr, out var year))
        {
            metadata.Year = year;
        }

        var dateStr = GetElementValue(root, "releasedate") ?? GetElementValue(root, "premiered");
        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
        {
            metadata.PublishedDate = date;
            metadata.Year ??= date.Year;
        }

        // Genres (can have multiple elements)
        foreach (var genre in root.Elements("genre"))
        {
            var value = genre.Value.Trim();
            if (!string.IsNullOrEmpty(value) && !metadata.Genres.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Genres.Add(value);
            }
        }

        // Tags
        foreach (var tag in root.Elements("tag"))
        {
            var value = tag.Value.Trim();
            if (!string.IsNullOrEmpty(value) && !metadata.Tags.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Tags.Add(value);
            }
        }

        // Rating
        var ratingStr = GetElementValue(root, "rating") ?? GetElementValue(root, "userrating");
        if (float.TryParse(ratingStr, out var rating))
        {
            metadata.CommunityRating = rating;
        }

        // Runtime (in minutes) -> convert to ticks
        var runtimeStr = GetElementValue(root, "runtime");
        if (int.TryParse(runtimeStr, out var runtimeMinutes))
        {
            metadata.DurationTicks = TimeSpan.FromMinutes(runtimeMinutes).Ticks;
        }

        // Language
        metadata.Language = GetElementValue(root, "language");

        // Series
        var setElement = root.Element("set");
        if (setElement is not null)
        {
            metadata.SeriesName = GetElementValue(setElement, "name") ?? setElement.Value.Trim();
        }

        metadata.SeriesName ??= GetElementValue(root, "series");

        var seriesIndexStr = GetElementValue(root, "seriesindex")
            ?? GetElementValue(root, "position");
        if (float.TryParse(seriesIndexStr, out var seriesIndex))
        {
            metadata.SeriesIndex = seriesIndex;
        }

        // Unique IDs
        foreach (var uniqueId in root.Elements("uniqueid"))
        {
            var type = uniqueId.Attribute("type")?.Value;
            var value = uniqueId.Value.Trim();

            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(value))
            {
                continue;
            }

            switch (type.ToLowerInvariant())
            {
                case "isbn":
                    if (value.Length == 13)
                    {
                        metadata.Isbn13 = value;
                    }
                    else
                    {
                        metadata.Isbn = value;
                    }

                    break;
                case "asin":
                    metadata.Asin = value;
                    break;
                case "audible":
                case "audible_asin":
                    metadata.AudibleAsin = value;
                    break;
                case "goodreads":
                    metadata.GoodreadsId = value;
                    break;
                case "googlebooks":
                case "google":
                    metadata.GoogleBooksId = value;
                    break;
                case "openlibrary":
                    metadata.OpenLibraryId = value;
                    break;
                default:
                    metadata.ProviderIds[type] = value;
                    break;
            }
        }

        // Cover image
        var thumb = GetElementValue(root, "thumb")
            ?? GetElementValue(root, "poster")
            ?? GetElementValue(root, "cover");
        if (!string.IsNullOrEmpty(thumb) && !string.IsNullOrEmpty(sourcePath))
        {
            var dir = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(dir))
            {
                var coverPath = Path.Combine(dir, thumb);
                if (File.Exists(coverPath))
                {
                    metadata.CoverImagePath = coverPath;
                }
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static string? GetElementValue(XElement parent, string elementName)
    {
        var value = parent.Element(elementName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddIfNotEmpty(List<string> list, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !list.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(value);
        }
    }
}
