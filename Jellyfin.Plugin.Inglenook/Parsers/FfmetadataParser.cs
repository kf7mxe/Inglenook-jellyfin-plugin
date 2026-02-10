using System.Text.RegularExpressions;
using Jellyfin.Plugin.Inglenook.Models;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Inglenook.Parsers;

/// <summary>
/// Parser for FFmpeg metadata format files.
/// </summary>
public partial class FfmetadataParser : IMetadataParser
{
    /// <inheritdoc />
    public string Name => "FFmetadata";

    /// <inheritdoc />
    public int Priority => 60;

    /// <inheritdoc />
    public string[] SupportedExtensions => new[] { ".ffmetadata", ".ffmeta" };

    /// <inheritdoc />
    public string[] FilePatterns => new[] { "FFMETADATA", "ffmetadata.txt" };

    [GeneratedRegex(@"^\[(\w+)\]$")]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^(\w+)=(.*)$")]
    private static partial Regex KeyValueRegex();

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

        var lines = content.Split('\n');
        if (lines.Length == 0)
        {
            return null;
        }

        // First line must be ;FFMETADATA1
        if (!lines[0].Trim().Equals(";FFMETADATA1", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "ffmetadata"
        };

        string? currentSection = null;
        var chapterData = new Dictionary<string, string>();
        var globalData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();

            // Skip comments
            if (line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            // Check for section header
            var sectionMatch = SectionRegex().Match(line);
            if (sectionMatch.Success)
            {
                // If we were in a CHAPTER section, finalize it
                if (string.Equals(currentSection, "CHAPTER", StringComparison.OrdinalIgnoreCase)
                    && chapterData.Count > 0)
                {
                    AddChapter(metadata, chapterData);
                    chapterData.Clear();
                }

                currentSection = sectionMatch.Groups[1].Value;
                continue;
            }

            // Parse key=value
            var kvMatch = KeyValueRegex().Match(line);
            if (!kvMatch.Success)
            {
                continue;
            }

            var key = kvMatch.Groups[1].Value;
            var value = UnescapeValue(kvMatch.Groups[2].Value);

            if (string.Equals(currentSection, "CHAPTER", StringComparison.OrdinalIgnoreCase))
            {
                chapterData[key] = value;
            }
            else if (currentSection is null)
            {
                globalData[key] = value;
            }
        }

        // Finalize last chapter if any
        if (string.Equals(currentSection, "CHAPTER", StringComparison.OrdinalIgnoreCase)
            && chapterData.Count > 0)
        {
            AddChapter(metadata, chapterData);
        }

        // Apply global metadata
        if (globalData.TryGetValue("title", out var title))
        {
            metadata.Title = title;
        }

        if (globalData.TryGetValue("artist", out var artist))
        {
            metadata.Authors.Add(artist);
        }

        if (globalData.TryGetValue("album_artist", out var albumArtist)
            && !metadata.Authors.Contains(albumArtist, StringComparer.OrdinalIgnoreCase))
        {
            metadata.Authors.Add(albumArtist);
        }

        if (globalData.TryGetValue("composer", out var composer))
        {
            metadata.Narrators.Add(composer);
        }

        if (globalData.TryGetValue("genre", out var genre))
        {
            metadata.Genres.Add(genre);
        }

        if (globalData.TryGetValue("date", out var date))
        {
            if (int.TryParse(date, out var year))
            {
                metadata.Year = year;
            }
            else if (DateTime.TryParse(date, out var dateTime))
            {
                metadata.PublishedDate = dateTime;
                metadata.Year = dateTime.Year;
            }
        }

        if (globalData.TryGetValue("publisher", out var publisher))
        {
            metadata.Publisher = publisher;
        }

        if (globalData.TryGetValue("description", out var desc))
        {
            metadata.Description = desc;
        }

        if (globalData.TryGetValue("comment", out var comment) && metadata.Description is null)
        {
            metadata.Description = comment;
        }

        if (globalData.TryGetValue("language", out var language))
        {
            metadata.Language = language;
        }

        return metadata.HasContent ? metadata : null;
    }

    private static void AddChapter(ParsedAudiobookMetadata metadata, Dictionary<string, string> chapterData)
    {
        // Determine timebase
        double timebaseFactor = 1000.0; // Default: milliseconds (TIMEBASE=1/1000)
        if (chapterData.TryGetValue("TIMEBASE", out var timebaseStr))
        {
            var parts = timebaseStr.Split('/');
            if (parts.Length == 2
                && double.TryParse(parts[0], out var num)
                && double.TryParse(parts[1], out var den)
                && den != 0)
            {
                timebaseFactor = den / num;
            }
        }

        if (!chapterData.TryGetValue("START", out var startStr)
            || !long.TryParse(startStr, out var startValue))
        {
            return;
        }

        var title = chapterData.GetValueOrDefault("title")
            ?? $"Chapter {metadata.Chapters.Count + 1}";

        // Convert to ticks: startValue / timebaseFactor gives seconds
        var seconds = startValue / timebaseFactor;
        var ticks = (long)(seconds * TimeSpan.TicksPerSecond);

        metadata.Chapters.Add(new ChapterInfo
        {
            Name = title,
            StartPositionTicks = ticks
        });
    }

    private static string UnescapeValue(string value)
    {
        return value
            .Replace("\\=", "=")
            .Replace("\\;", ";")
            .Replace("\\#", "#")
            .Replace("\\\\", "\\")
            .Replace("\\n", "\n");
    }
}
