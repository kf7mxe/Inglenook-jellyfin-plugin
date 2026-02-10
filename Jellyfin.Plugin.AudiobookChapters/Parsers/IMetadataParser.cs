using Jellyfin.Plugin.AudiobookChapters.Models;

namespace Jellyfin.Plugin.AudiobookChapters.Parsers;

/// <summary>
/// Interface for all audiobook metadata parsers.
/// </summary>
public interface IMetadataParser
{
    /// <summary>Gets the display name of this parser.</summary>
    string Name { get; }

    /// <summary>Gets the priority of this parser. Higher values are more trusted.</summary>
    int Priority { get; }

    /// <summary>Gets the file extensions this parser supports (e.g., ".cue", ".opf").</summary>
    string[] SupportedExtensions { get; }

    /// <summary>Gets specific filenames this parser looks for (e.g., "metadata.json").</summary>
    string[] FilePatterns { get; }

    /// <summary>
    /// Determines whether this parser can handle the given file.
    /// </summary>
    bool CanParse(string filePath);

    /// <summary>
    /// Parses metadata from the given file asynchronously.
    /// </summary>
    Task<ParsedAudiobookMetadata?> ParseAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Parses metadata from raw string content.
    /// </summary>
    ParsedAudiobookMetadata? ParseContent(string content, string? sourcePath);
}
