using System.Text.RegularExpressions;
using Jellyfin.Plugin.Inglenook.Models;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Inglenook.Parsers;

/// <summary>
/// Parser for simple text metadata files.
/// </summary>
public partial class SimpleTextParser : IMetadataParser
{
    /// <inheritdoc />
    public string Name => "Simple Text";

    /// <inheritdoc />
    public int Priority => 30;

    /// <inheritdoc />
    public string[] SupportedExtensions => new[] { ".txt" };

    /// <inheritdoc />
    public string[] FilePatterns => new[]
    {
        "desc.txt", "description.txt", "reader.txt", "narrator.txt",
        "info.txt", "about.txt", "chapters.txt", "book.txt"
    };

    // Timestamp patterns: "HH:MM:SS Title", "H:MM:SS Title", "HH:MM:SS.mmm Title", "[HH:MM:SS] Title", "MM:SS Title"
    [GeneratedRegex(@"^\[?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,3}))?\]?\s+(.+)$")]
    private static partial Regex TimestampHmsRegex();

    [GeneratedRegex(@"^\[?(\d{1,2}):(\d{2})\]?\s+(.+)$")]
    private static partial Regex TimestampMsRegex();

    [GeneratedRegex(@"^([^:]+):\s*(.+)$")]
    private static partial Regex KeyValueRegex();

    /// <inheritdoc />
    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (!string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
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

        var fileName = !string.IsNullOrEmpty(sourcePath)
            ? Path.GetFileName(sourcePath).ToLowerInvariant()
            : string.Empty;

        return fileName switch
        {
            "chapters.txt" => ParseChaptersFile(content, sourcePath),
            "reader.txt" or "narrator.txt" => ParseNarratorFile(content, sourcePath),
            "desc.txt" or "description.txt" or "about.txt" => ParseDescriptionFile(content, sourcePath),
            "info.txt" or "book.txt" => ParseInfoFile(content, sourcePath),
            _ => null
        };
    }

    private static ParsedAudiobookMetadata? ParseChaptersFile(string content, string? sourcePath)
    {
        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "txt"
        };

        var lines = content.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Try HH:MM:SS format first
            var hmsMatch = TimestampHmsRegex().Match(line);
            if (hmsMatch.Success)
            {
                var hours = int.Parse(hmsMatch.Groups[1].Value);
                var minutes = int.Parse(hmsMatch.Groups[2].Value);
                var seconds = int.Parse(hmsMatch.Groups[3].Value);
                var milliseconds = 0;
                if (hmsMatch.Groups[4].Success)
                {
                    var msStr = hmsMatch.Groups[4].Value.PadRight(3, '0');
                    milliseconds = int.Parse(msStr);
                }

                var title = hmsMatch.Groups[5].Value.Trim();
                var timespan = new TimeSpan(0, hours, minutes, seconds, milliseconds);

                metadata.Chapters.Add(new ChapterInfo
                {
                    Name = title,
                    StartPositionTicks = timespan.Ticks
                });

                continue;
            }

            // Try MM:SS format
            var msMatch = TimestampMsRegex().Match(line);
            if (msMatch.Success)
            {
                var minutes = int.Parse(msMatch.Groups[1].Value);
                var seconds = int.Parse(msMatch.Groups[2].Value);
                var title = msMatch.Groups[3].Value.Trim();
                var timespan = new TimeSpan(0, 0, minutes, seconds);

                metadata.Chapters.Add(new ChapterInfo
                {
                    Name = title,
                    StartPositionTicks = timespan.Ticks
                });
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static ParsedAudiobookMetadata? ParseNarratorFile(string content, string? sourcePath)
    {
        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "txt"
        };

        var lines = content.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (!metadata.Narrators.Contains(line, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Narrators.Add(line);
            }
        }

        return metadata.HasContent ? metadata : null;
    }

    private static ParsedAudiobookMetadata? ParseDescriptionFile(string content, string? sourcePath)
    {
        var trimmed = content.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "txt",
            Description = trimmed
        };
    }

    private static ParsedAudiobookMetadata? ParseInfoFile(string content, string? sourcePath)
    {
        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "txt"
        };

        var lines = content.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var descriptionLines = new List<string>();
        var inDescription = false;

        foreach (var line in lines)
        {
            if (inDescription)
            {
                descriptionLines.Add(line);
                continue;
            }

            var kvMatch = KeyValueRegex().Match(line);
            if (!kvMatch.Success)
            {
                continue;
            }

            var key = kvMatch.Groups[1].Value.Trim().ToLowerInvariant();
            var value = kvMatch.Groups[2].Value.Trim();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            switch (key)
            {
                case "title":
                    metadata.Title = value;
                    break;
                case "author":
                case "writer":
                    metadata.Authors.Add(value);
                    break;
                case "narrator":
                case "reader":
                    metadata.Narrators.Add(value);
                    break;
                case "publisher":
                    metadata.Publisher = value;
                    break;
                case "year":
                    if (int.TryParse(value, out var year))
                    {
                        metadata.Year = year;
                    }

                    break;
                case "date":
                    if (DateTime.TryParse(value, out var date))
                    {
                        metadata.PublishedDate = date;
                        metadata.Year ??= date.Year;
                    }

                    break;
                case "genre":
                    foreach (var g in value.Split(',', StringSplitOptions.TrimEntries))
                    {
                        if (!string.IsNullOrEmpty(g) && !metadata.Genres.Contains(g, StringComparer.OrdinalIgnoreCase))
                        {
                            metadata.Genres.Add(g);
                        }
                    }

                    break;
                case "series":
                    metadata.SeriesName = value;
                    break;
                case "duration":
                    if (TimeSpan.TryParse(value, out var duration))
                    {
                        metadata.DurationTicks = duration.Ticks;
                    }

                    break;
                case "language":
                    metadata.Language = value;
                    break;
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
                case "description":
                    inDescription = true;
                    descriptionLines.Add(value);
                    break;
                case "abridged":
                    metadata.Abridged = string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        if (descriptionLines.Count > 0)
        {
            metadata.Description = string.Join("\n", descriptionLines);
        }

        return metadata.HasContent ? metadata : null;
    }
}
