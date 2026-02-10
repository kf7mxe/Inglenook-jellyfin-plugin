using System.Text.RegularExpressions;
using Jellyfin.Plugin.AudiobookChapters.Configuration;
using Jellyfin.Plugin.AudiobookChapters.Models;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudiobookChapters.Parsers;

/// <summary>
/// Detects and processes multi-file audiobooks where each chapter is a separate audio file.
/// </summary>
public partial class MultiFileAudiobookHandler
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".m4b", ".flac", ".ogg", ".opus", ".wma", ".aac", ".wav", ".aiff"
    };

    private readonly ILogger<MultiFileAudiobookHandler> _logger;

    // Pattern: "01 - Title" or "01. Title"
    [GeneratedRegex(@"^(\d{1,3})\s*[-\.]\s*(.+)$")]
    private static partial Regex NumberDashTitleRegex();

    // Pattern: "Chapter 01 - Title" or "Chapter 1"
    [GeneratedRegex(@"^(?:Chapter|Ch\.?)\s*(\d{1,3})(?:\s*[-\.]\s*(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterRegex();

    // Pattern: "Part 1 - Title"
    [GeneratedRegex(@"^Part\s*(\d{1,3})(?:\s*[-\.]\s*(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex PartRegex();

    // Pattern: "Track 01"
    [GeneratedRegex(@"^Track\s*(\d{1,3})(?:\s*[-\.]\s*(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex TrackRegex();

    // Pattern: "Disc 1 - 01 - Title" (multi-disc)
    [GeneratedRegex(@"^(?:Disc|Disk|CD)\s*(\d{1,2})\s*[-\.]\s*(\d{1,3})(?:\s*[-\.]\s*(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscTrackRegex();

    // Pattern: Simple number prefix "1 Title"
    [GeneratedRegex(@"^(\d{1,3})\s+(.+)$")]
    private static partial Regex SimpleNumberRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiFileAudiobookHandler"/> class.
    /// </summary>
    public MultiFileAudiobookHandler(ILogger<MultiFileAudiobookHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines whether the given directory contains a multi-file audiobook.
    /// </summary>
    public bool IsMultiFileAudiobook(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        var audioFiles = Directory.GetFiles(directoryPath)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (audioFiles.Count < 2)
        {
            return false;
        }

        // Check that at least 70% of files match a numbering pattern
        var matchingCount = audioFiles.Count(f =>
        {
            var (trackNumber, _) = ParseFilename(Path.GetFileNameWithoutExtension(f));
            return trackNumber.HasValue;
        });

        return matchingCount >= audioFiles.Count * 0.7;
    }

    /// <summary>
    /// Gets audio files in the directory sorted by track number.
    /// </summary>
    public List<AudiobookFile> GetAudioFilesInOrder(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new List<AudiobookFile>();
        }

        var audioFiles = Directory.GetFiles(directoryPath)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
            .Select(f =>
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(f);
                var (trackNumber, title) = ParseFilename(nameWithoutExt);
                return new AudiobookFile
                {
                    Path = f,
                    Name = nameWithoutExt,
                    TrackNumber = trackNumber,
                    Title = title
                };
            })
            .OrderBy(f => f.TrackNumber ?? int.MaxValue)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Assign sort order
        for (var i = 0; i < audioFiles.Count; i++)
        {
            audioFiles[i].SortOrder = i;
        }

        return audioFiles;
    }

    /// <summary>
    /// Creates chapter entries from a list of audio files.
    /// </summary>
    public List<ChapterInfo> CreateChaptersFromFiles(List<AudiobookFile> files, MultiFileNamingStrategy strategy)
    {
        var chapters = new List<ChapterInfo>();
        long cumulativeTicks = 0;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            file.StartPositionTicks = cumulativeTicks;

            var chapterName = strategy switch
            {
                MultiFileNamingStrategy.UseMetadataTitle => file.Title ?? CleanupChapterName(file.Name),
                MultiFileNamingStrategy.UseSequentialNumbering => $"Chapter {i + 1}",
                MultiFileNamingStrategy.ParseFilenamePattern => file.Title ?? CleanupChapterName(file.Name),
                _ => CleanupChapterName(file.Name) // UseFilename
            };

            chapters.Add(new ChapterInfo
            {
                Name = chapterName,
                StartPositionTicks = cumulativeTicks
            });

            cumulativeTicks += file.DurationTicks;
        }

        return chapters;
    }

    /// <summary>
    /// Creates metadata from a directory of audio files.
    /// </summary>
    public Task<ParsedAudiobookMetadata?> CreateMetadataFromDirectoryAsync(
        string directoryPath,
        MultiFileNamingStrategy namingStrategy,
        CancellationToken cancellationToken)
    {
        if (!IsMultiFileAudiobook(directoryPath))
        {
            return Task.FromResult<ParsedAudiobookMetadata?>(null);
        }

        var files = GetAudioFilesInOrder(directoryPath);
        if (files.Count == 0)
        {
            return Task.FromResult<ParsedAudiobookMetadata?>(null);
        }

        _logger.LogDebug("Found {Count} audio files in multi-file audiobook: {Path}", files.Count, directoryPath);

        var chapters = CreateChaptersFromFiles(files, namingStrategy);

        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = directoryPath,
            SourceType = "multifile",
            Chapters = chapters
        };

        // Use directory name as a fallback title
        var dirName = Path.GetFileName(directoryPath);
        if (!string.IsNullOrEmpty(dirName))
        {
            metadata.Title = dirName;
        }

        return Task.FromResult<ParsedAudiobookMetadata?>(metadata);
    }

    /// <summary>
    /// Parses a filename to extract track number and title.
    /// </summary>
    public static (int? TrackNumber, string? Title) ParseFilename(string filename)
    {
        // Try each pattern in order of specificity

        // Disc/Track pattern: "Disc 1 - 01 - Title"
        var discMatch = DiscTrackRegex().Match(filename);
        if (discMatch.Success)
        {
            var disc = int.Parse(discMatch.Groups[1].Value);
            var track = int.Parse(discMatch.Groups[2].Value);
            var title = discMatch.Groups[3].Success ? discMatch.Groups[3].Value.Trim() : null;
            // Combine disc and track for sorting: disc * 1000 + track
            return (disc * 1000 + track, title);
        }

        // Chapter pattern: "Chapter 01 - Title"
        var chapterMatch = ChapterRegex().Match(filename);
        if (chapterMatch.Success)
        {
            var track = int.Parse(chapterMatch.Groups[1].Value);
            var title = chapterMatch.Groups[2].Success ? chapterMatch.Groups[2].Value.Trim() : null;
            return (track, title);
        }

        // Part pattern: "Part 1 - Title"
        var partMatch = PartRegex().Match(filename);
        if (partMatch.Success)
        {
            var track = int.Parse(partMatch.Groups[1].Value);
            var title = partMatch.Groups[2].Success ? partMatch.Groups[2].Value.Trim() : null;
            return (track, title);
        }

        // Track pattern: "Track 01"
        var trackMatch = TrackRegex().Match(filename);
        if (trackMatch.Success)
        {
            var track = int.Parse(trackMatch.Groups[1].Value);
            var title = trackMatch.Groups[2].Success ? trackMatch.Groups[2].Value.Trim() : null;
            return (track, title);
        }

        // Number-dash-title: "01 - Title" or "01. Title"
        var numDashMatch = NumberDashTitleRegex().Match(filename);
        if (numDashMatch.Success)
        {
            var track = int.Parse(numDashMatch.Groups[1].Value);
            var title = numDashMatch.Groups[2].Value.Trim();
            return (track, title);
        }

        // Simple number prefix: "1 Title"
        var simpleMatch = SimpleNumberRegex().Match(filename);
        if (simpleMatch.Success)
        {
            var track = int.Parse(simpleMatch.Groups[1].Value);
            var title = simpleMatch.Groups[2].Value.Trim();
            return (track, title);
        }

        return (null, null);
    }

    /// <summary>
    /// Cleans up a chapter name by removing track numbers and common prefixes.
    /// </summary>
    public static string CleanupChapterName(string name)
    {
        // Remove leading numbers with separators
        var cleaned = Regex.Replace(name, @"^\d{1,3}\s*[-\.]\s*", string.Empty);

        // Remove "Chapter X - ", "Part X - ", "Track X - " prefixes
        cleaned = Regex.Replace(cleaned, @"^(?:Chapter|Ch\.?|Part|Track)\s*\d{1,3}\s*[-\.]\s*", string.Empty, RegexOptions.IgnoreCase);

        // Remove "Disc X - " prefix
        cleaned = Regex.Replace(cleaned, @"^(?:Disc|Disk|CD)\s*\d{1,2}\s*[-\.]\s*\d{1,3}\s*[-\.]\s*", string.Empty, RegexOptions.IgnoreCase);

        cleaned = cleaned.Trim();

        // If cleaned is empty, return original name
        return string.IsNullOrEmpty(cleaned) ? name.Trim() : cleaned;
    }
}
