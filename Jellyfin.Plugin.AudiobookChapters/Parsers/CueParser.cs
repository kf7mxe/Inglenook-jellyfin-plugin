using System.Text.RegularExpressions;
using Jellyfin.Plugin.AudiobookChapters.Models;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AudiobookChapters.Parsers;

/// <summary>
/// Parser for CUE sheet files.
/// </summary>
public partial class CueParser : IMetadataParser
{
    /// <inheritdoc />
    public string Name => "CUE Sheet";

    /// <inheritdoc />
    public int Priority => 50;

    /// <inheritdoc />
    public string[] SupportedExtensions => new[] { ".cue" };

    /// <inheritdoc />
    public string[] FilePatterns => Array.Empty<string>();

    [GeneratedRegex(@"^REM\s+GENRE\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RemGenreRegex();

    [GeneratedRegex(@"^REM\s+DATE\s+(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RemDateRegex();

    [GeneratedRegex(@"^REM\s+COMMENT\s+""?(.+?)""?$", RegexOptions.IgnoreCase)]
    private static partial Regex RemCommentRegex();

    [GeneratedRegex(@"^PERFORMER\s+""?(.+?)""?$", RegexOptions.IgnoreCase)]
    private static partial Regex PerformerRegex();

    [GeneratedRegex(@"^TITLE\s+""?(.+?)""?$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^SONGWRITER\s+""?(.+?)""?$", RegexOptions.IgnoreCase)]
    private static partial Regex SongwriterRegex();

    [GeneratedRegex(@"^TRACK\s+(\d+)\s+AUDIO$", RegexOptions.IgnoreCase)]
    private static partial Regex TrackRegex();

    [GeneratedRegex(@"^INDEX\s+01\s+(\d+):(\d+):(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex IndexRegex();

    [GeneratedRegex(@"^ISRC\s+(\S+)$", RegexOptions.IgnoreCase)]
    private static partial Regex IsrcRegex();

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

        var metadata = new ParsedAudiobookMetadata
        {
            SourceFile = sourcePath,
            SourceType = "cue"
        };

        var lines = content.Split('\n', StringSplitOptions.TrimEntries);
        var inTrack = false;
        string? currentTrackTitle = null;
        string? currentTrackPerformer = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var trackMatch = TrackRegex().Match(line);
            if (trackMatch.Success)
            {
                inTrack = true;
                currentTrackTitle = null;
                currentTrackPerformer = null;
                continue;
            }

            if (inTrack)
            {
                var titleMatch = TitleRegex().Match(line);
                if (titleMatch.Success)
                {
                    currentTrackTitle = titleMatch.Groups[1].Value;
                    continue;
                }

                var performerMatch = PerformerRegex().Match(line);
                if (performerMatch.Success)
                {
                    currentTrackPerformer = performerMatch.Groups[1].Value;
                    continue;
                }

                var indexMatch = IndexRegex().Match(line);
                if (indexMatch.Success)
                {
                    var minutes = int.Parse(indexMatch.Groups[1].Value);
                    var seconds = int.Parse(indexMatch.Groups[2].Value);
                    var frames = int.Parse(indexMatch.Groups[3].Value);

                    // CUE frames are 1/75 second
                    var totalSeconds = (minutes * 60.0) + seconds + (frames / 75.0);
                    var ticks = (long)(totalSeconds * TimeSpan.TicksPerSecond);

                    var chapterName = currentTrackTitle
                        ?? $"Chapter {metadata.Chapters.Count + 1}";

                    metadata.Chapters.Add(new ChapterInfo
                    {
                        Name = chapterName,
                        StartPositionTicks = ticks
                    });

                    continue;
                }

                var songwriterMatch = SongwriterRegex().Match(line);
                if (songwriterMatch.Success)
                {
                    var narrator = songwriterMatch.Groups[1].Value;
                    if (!metadata.Narrators.Contains(narrator, StringComparer.OrdinalIgnoreCase))
                    {
                        metadata.Narrators.Add(narrator);
                    }

                    continue;
                }
            }
            else
            {
                // Album-level metadata
                var titleMatch = TitleRegex().Match(line);
                if (titleMatch.Success)
                {
                    metadata.Title = titleMatch.Groups[1].Value;
                    continue;
                }

                var performerMatch = PerformerRegex().Match(line);
                if (performerMatch.Success)
                {
                    var author = performerMatch.Groups[1].Value;
                    if (!metadata.Authors.Contains(author, StringComparer.OrdinalIgnoreCase))
                    {
                        metadata.Authors.Add(author);
                    }

                    continue;
                }

                var genreMatch = RemGenreRegex().Match(line);
                if (genreMatch.Success)
                {
                    metadata.Genres.Add(genreMatch.Groups[1].Value.Trim());
                    continue;
                }

                var dateMatch = RemDateRegex().Match(line);
                if (dateMatch.Success)
                {
                    if (int.TryParse(dateMatch.Groups[1].Value, out var year))
                    {
                        metadata.Year = year;
                    }

                    continue;
                }

                var commentMatch = RemCommentRegex().Match(line);
                if (commentMatch.Success)
                {
                    metadata.Description = commentMatch.Groups[1].Value;
                    continue;
                }

                var songwriterMatch = SongwriterRegex().Match(line);
                if (songwriterMatch.Success)
                {
                    var narrator = songwriterMatch.Groups[1].Value;
                    if (!metadata.Narrators.Contains(narrator, StringComparer.OrdinalIgnoreCase))
                    {
                        metadata.Narrators.Add(narrator);
                    }

                    continue;
                }
            }
        }

        return metadata.HasContent ? metadata : null;
    }
}
