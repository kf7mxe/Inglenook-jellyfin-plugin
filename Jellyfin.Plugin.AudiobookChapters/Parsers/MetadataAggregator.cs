using Jellyfin.Plugin.AudiobookChapters.Configuration;
using Jellyfin.Plugin.AudiobookChapters.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudiobookChapters.Parsers;

/// <summary>
/// Orchestrates metadata parsing from multiple sources and merges results.
/// </summary>
public class MetadataAggregator
{
    private readonly ILogger<MetadataAggregator> _logger;
    private readonly List<IMetadataParser> _parsers;
    private readonly MultiFileAudiobookHandler _multiFileHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataAggregator"/> class.
    /// </summary>
    public MetadataAggregator(
        ILogger<MetadataAggregator> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _parsers = new List<IMetadataParser>
        {
            new OpfParser(),
            new JsonMetadataParser(),
            new NfoParser(),
            new FfmetadataParser(),
            new CueParser(),
            new SimpleTextParser()
        };
        _multiFileHandler = new MultiFileAudiobookHandler(
            loggerFactory.CreateLogger<MultiFileAudiobookHandler>());
    }

    /// <summary>
    /// Gets all metadata for an audiobook item.
    /// </summary>
    public async Task<ParsedAudiobookMetadata?> GetMetadataAsync(
        string itemPath,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var directory = File.Exists(itemPath)
            ? Path.GetDirectoryName(itemPath)
            : Directory.Exists(itemPath) ? itemPath : null;

        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        var results = new List<ParsedAudiobookMetadata>();

        // Find and parse all metadata files
        var metadataFiles = FindMetadataFiles(directory, config);
        foreach (var (parser, filePath) in metadataFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
                if (result is not null && result.HasContent)
                {
                    _logger.LogDebug("Parsed metadata from {Parser}: {File}", parser.Name, filePath);
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse {File} with {Parser}", filePath, parser.Name);
            }
        }

        // Multi-file audiobook detection
        if (config.EnableMultiFileDetection)
        {
            try
            {
                var multiFileResult = await _multiFileHandler.CreateMetadataFromDirectoryAsync(
                    directory,
                    config.MultiFileChapterNaming,
                    cancellationToken).ConfigureAwait(false);

                if (multiFileResult is not null)
                {
                    _logger.LogDebug("Detected multi-file audiobook in {Directory}", directory);
                    results.Add(multiFileResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed multi-file detection for {Directory}", directory);
            }
        }

        if (results.Count == 0)
        {
            return null;
        }

        if (results.Count == 1)
        {
            return results[0];
        }

        // Merge metadata from all sources
        var priorityOrder = ParsePriorityOrder(config.MetadataPriority);
        return MergeMetadata(results, priorityOrder);
    }

    /// <summary>
    /// Finds all metadata files in a directory that can be parsed.
    /// </summary>
    public List<(IMetadataParser Parser, string FilePath)> FindMetadataFiles(
        string directory,
        PluginConfiguration config)
    {
        var found = new List<(IMetadataParser, string)>();

        if (!Directory.Exists(directory))
        {
            return found;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory);
        }
        catch
        {
            return found;
        }

        foreach (var parser in _parsers)
        {
            if (!IsParserEnabled(parser, config))
            {
                continue;
            }

            foreach (var file in files)
            {
                if (parser.CanParse(file))
                {
                    found.Add((parser, file));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Checks whether a parser is enabled in the configuration.
    /// </summary>
    public static bool IsParserEnabled(IMetadataParser parser, PluginConfiguration config)
    {
        return parser switch
        {
            CueParser => config.EnableCueFiles,
            OpfParser => config.EnableOpfFiles,
            JsonMetadataParser => config.EnableJsonMetadata,
            NfoParser => config.EnableNfoFiles,
            FfmetadataParser => config.EnableFfmetadata,
            SimpleTextParser => config.EnableTextFiles,
            _ => true
        };
    }

    /// <summary>
    /// Parses a comma-separated priority string into an ordered list of source types.
    /// </summary>
    public static List<string> ParsePriorityOrder(string priorityString)
    {
        if (string.IsNullOrWhiteSpace(priorityString))
        {
            return new List<string> { "opf", "json", "nfo", "cue", "ffmetadata", "txt" };
        }

        return priorityString
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.ToLowerInvariant())
            .ToList();
    }

    /// <summary>
    /// Merges metadata from multiple sources using priority ordering.
    /// </summary>
    public static ParsedAudiobookMetadata MergeMetadata(
        List<ParsedAudiobookMetadata> sources,
        List<string> priorityOrder)
    {
        // Sort by priority
        var sorted = sources
            .OrderBy(s =>
            {
                var idx = priorityOrder.IndexOf(s.SourceType?.ToLowerInvariant() ?? string.Empty);
                return idx >= 0 ? idx : priorityOrder.Count;
            })
            .ToList();

        var merged = new ParsedAudiobookMetadata
        {
            SourceType = "merged"
        };

        foreach (var source in sorted)
        {
            // Scalar fields: first non-null wins
            merged.Title ??= source.Title;
            merged.SortTitle ??= source.SortTitle;
            merged.OriginalTitle ??= source.OriginalTitle;
            merged.Subtitle ??= source.Subtitle;
            merged.Description ??= source.Description;
            merged.Publisher ??= source.Publisher;
            merged.PublishedDate ??= source.PublishedDate;
            merged.Year ??= source.Year;
            merged.Language ??= source.Language;
            merged.CommunityRating ??= source.CommunityRating;
            merged.CriticRating ??= source.CriticRating;
            merged.Abridged ??= source.Abridged;
            merged.SeriesName ??= source.SeriesName;
            merged.SeriesIndex ??= source.SeriesIndex;
            merged.Isbn ??= source.Isbn;
            merged.Isbn13 ??= source.Isbn13;
            merged.Asin ??= source.Asin;
            merged.AudibleAsin ??= source.AudibleAsin;
            merged.GoodreadsId ??= source.GoodreadsId;
            merged.GoogleBooksId ??= source.GoogleBooksId;
            merged.OpenLibraryId ??= source.OpenLibraryId;
            merged.DurationTicks ??= source.DurationTicks;
            merged.CoverImagePath ??= source.CoverImagePath;

            // Lists: combine unique values
            MergeList(merged.Authors, source.Authors);
            MergeList(merged.Narrators, source.Narrators);
            MergeList(merged.Genres, source.Genres);
            MergeList(merged.Tags, source.Tags);

            // Provider IDs: first value wins per key
            foreach (var kvp in source.ProviderIds)
            {
                merged.ProviderIds.TryAdd(kvp.Key, kvp.Value);
            }
        }

        // Chapters: prefer explicit chapter sources over multi-file, then prefer more chapters
        var chapterSources = sorted
            .Where(s => s.HasChapters)
            .OrderBy(s => s.SourceType == "multifile" ? 1 : 0) // Prefer explicit over multi-file
            .ThenByDescending(s => s.Chapters.Count) // Then prefer more chapters
            .ToList();

        if (chapterSources.Count > 0)
        {
            merged.Chapters = chapterSources[0].Chapters;
        }

        return merged;
    }

    private static void MergeList(List<string> target, List<string> source)
    {
        foreach (var item in source)
        {
            if (!target.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(item);
            }
        }
    }
}
