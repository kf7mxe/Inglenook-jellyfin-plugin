using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudiobookChapters.Configuration;

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

    /// <summary>Gets or sets the comma-separated priority order for metadata sources.</summary>
    public string MetadataPriority { get; set; } = "opf,json,nfo,cue,ffmetadata,txt";

    /// <summary>Gets or sets the list of library IDs the plugin should scan. Empty means all libraries.</summary>
    public string[] EnabledLibraryIds { get; set; } = Array.Empty<string>();
}
