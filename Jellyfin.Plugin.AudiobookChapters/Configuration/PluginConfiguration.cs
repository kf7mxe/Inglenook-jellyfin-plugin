using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudiobookChapters.Configuration;

/// <summary>
/// Strategy for naming chapters in multi-file audiobooks.
/// </summary>
public enum MultiFileNamingStrategy
{
    /// <summary>Use the filename (cleaned up) as the chapter title.</summary>
    UseFilename = 0,

    /// <summary>Use embedded metadata title if available.</summary>
    UseMetadataTitle = 1,

    /// <summary>Use sequential numbering (Chapter 1, Chapter 2, etc.).</summary>
    UseSequentialNumbering = 2,

    /// <summary>Parse the filename using a pattern to extract the title.</summary>
    ParseFilenamePattern = 3
}

/// <summary>
/// Plugin configuration for Audiobook Chapters.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets a value indicating whether CUE file parsing is enabled.</summary>
    public bool EnableCueFiles { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether OPF file parsing is enabled.</summary>
    public bool EnableOpfFiles { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether JSON metadata file parsing is enabled.</summary>
    public bool EnableJsonMetadata { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether NFO file parsing is enabled.</summary>
    public bool EnableNfoFiles { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether FFmetadata file parsing is enabled.</summary>
    public bool EnableFfmetadata { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether text file parsing is enabled.</summary>
    public bool EnableTextFiles { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether multi-file audiobook detection is enabled.</summary>
    public bool EnableMultiFileDetection { get; set; } = true;

    /// <summary>Gets or sets the naming strategy for multi-file audiobook chapters.</summary>
    public MultiFileNamingStrategy MultiFileChapterNaming { get; set; } = MultiFileNamingStrategy.UseFilename;

    /// <summary>Gets or sets the comma-separated priority order for metadata sources.</summary>
    public string MetadataPriority { get; set; } = "opf,json,nfo,cue,ffmetadata,txt";

    /// <summary>Gets or sets the list of library IDs the plugin should scan. Empty means all libraries.</summary>
    public string[] EnabledLibraryIds { get; set; } = Array.Empty<string>();
}
