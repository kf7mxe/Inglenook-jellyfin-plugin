using System.Text.RegularExpressions;
using System.Xml.Linq;
using Jellyfin.Plugin.AudiobookChapters.Models;

namespace Jellyfin.Plugin.AudiobookChapters.Parsers;

/// <summary>
/// Parser for OPF (Calibre/EPUB) metadata files.
/// </summary>
public partial class OpfParser : IMetadataParser
{
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace OpfNs = "http://www.idpf.org/2007/opf";

    /// <inheritdoc />
    public string Name => "OPF (Calibre/EPUB)";

    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public string[] SupportedExtensions => new[] { ".opf" };

    /// <inheritdoc />
    public string[] FilePatterns => new[] { "metadata.opf", "content.opf" };

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    /// <inheritdoc />
    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return true;
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

        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch
        {
            return null;
        }

        if (doc.Root is null)
        {
            return null;
        }

        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "opf"
        };

        var metadataElement = doc.Root.Element(OpfNs + "metadata")
            ?? doc.Root.Element("metadata");

        if (metadataElement is null)
        {
            return null;
        }

        // Title
        metadata.Title = GetDcElementValue(metadataElement, "title");

        // Creators (authors and narrators)
        var creators = metadataElement.Elements(DcNs + "creator")
            .Concat(metadataElement.Elements("creator"));

        foreach (var creator in creators)
        {
            var role = creator.Attribute(OpfNs + "role")?.Value
                ?? creator.Attribute("role")?.Value;
            var name = creator.Value.Trim();

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (string.Equals(role, "nrt", StringComparison.OrdinalIgnoreCase))
            {
                if (!metadata.Narrators.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Narrators.Add(name);
                }
            }
            else
            {
                // Default to author (role "aut" or no role specified)
                if (!metadata.Authors.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    metadata.Authors.Add(name);
                }
            }
        }

        // Description - strip HTML tags
        var description = GetDcElementValue(metadataElement, "description");
        if (!string.IsNullOrEmpty(description))
        {
            metadata.Description = StripHtml(description);
        }

        // Publisher
        metadata.Publisher = GetDcElementValue(metadataElement, "publisher");

        // Date
        var dateStr = GetDcElementValue(metadataElement, "date");
        if (!string.IsNullOrEmpty(dateStr))
        {
            if (DateTime.TryParse(dateStr, out var date))
            {
                metadata.PublishedDate = date;
                metadata.Year = date.Year;
            }
            else if (int.TryParse(dateStr, out var year))
            {
                metadata.Year = year;
            }
        }

        // Language
        metadata.Language = GetDcElementValue(metadataElement, "language");

        // Subjects (genres)
        var subjects = metadataElement.Elements(DcNs + "subject")
            .Concat(metadataElement.Elements("subject"));
        foreach (var subject in subjects)
        {
            var value = subject.Value.Trim();
            if (!string.IsNullOrEmpty(value) && !metadata.Genres.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Genres.Add(value);
            }
        }

        // Identifiers (ISBN, ASIN, etc.)
        var identifiers = metadataElement.Elements(DcNs + "identifier")
            .Concat(metadataElement.Elements("identifier"));
        foreach (var identifier in identifiers)
        {
            var scheme = identifier.Attribute(OpfNs + "scheme")?.Value
                ?? identifier.Attribute("scheme")?.Value;
            var value = identifier.Value.Trim();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (string.IsNullOrEmpty(scheme))
            {
                // Try to detect scheme from value format
                if (value.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase))
                {
                    value = value[9..];
                    scheme = "isbn";
                }
                else if (value.Length == 10 && value.StartsWith("B", StringComparison.Ordinal))
                {
                    scheme = "asin";
                }
            }

            switch (scheme?.ToLowerInvariant())
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
                case "amazon":
                    metadata.Asin = value;
                    break;
                case "audible":
                case "audible_asin":
                    metadata.AudibleAsin = value;
                    break;
                case "goodreads":
                    metadata.GoodreadsId = value;
                    break;
                case "google":
                case "google-books":
                    metadata.GoogleBooksId = value;
                    break;
                case "openlibrary":
                    metadata.OpenLibraryId = value;
                    break;
                default:
                    if (!string.IsNullOrEmpty(scheme))
                    {
                        metadata.ProviderIds[scheme] = value;
                    }

                    break;
            }
        }

        // Calibre-specific meta tags
        var metaElements = metadataElement.Elements(OpfNs + "meta")
            .Concat(metadataElement.Elements("meta"));
        foreach (var meta in metaElements)
        {
            var name = meta.Attribute("name")?.Value;
            var metaContent = meta.Attribute("content")?.Value;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(metaContent))
            {
                continue;
            }

            switch (name)
            {
                case "calibre:series":
                    metadata.SeriesName = metaContent;
                    break;
                case "calibre:series_index":
                    if (float.TryParse(metaContent, out var seriesIndex))
                    {
                        metadata.SeriesIndex = seriesIndex;
                    }

                    break;
                case "calibre:rating":
                    if (float.TryParse(metaContent, out var rating))
                    {
                        metadata.CommunityRating = rating;
                    }

                    break;
                case "calibre:title_sort":
                    metadata.SortTitle = metaContent;
                    break;
            }
        }

        // Look for cover image in same directory
        if (!string.IsNullOrEmpty(sourcePath))
        {
            var dir = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(dir))
            {
                var coverNames = new[] { "cover.jpg", "cover.png", "cover.jpeg", "folder.jpg", "folder.png" };
                foreach (var coverName in coverNames)
                {
                    var coverPath = Path.Combine(dir, coverName);
                    if (File.Exists(coverPath))
                    {
                        metadata.CoverImagePath = coverPath;
                        break;
                    }
                }
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static string? GetDcElementValue(XElement metadataElement, string localName)
    {
        var value = metadataElement.Element(DcNs + localName)?.Value
            ?? metadataElement.Element(localName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string StripHtml(string html)
    {
        var text = HtmlTagRegex().Replace(html, string.Empty);
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
}
