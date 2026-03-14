using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Inglenook.Configuration;

/// <summary>
/// Plugin configuration for Inglenook.
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

    /// <summary>Gets or sets a value indicating whether SPA web client serving is enabled.</summary>
    public bool EnableSpaServing { get; set; } = true;

    /// <summary>Gets or sets the GitHub repository for the SPA web client (owner/repo format).</summary>
    public string SpaGitHubRepo { get; set; } = "kf7mxe/inglenook";

    /// <summary>Gets or sets a value indicating whether automatic SPA updates are enabled.</summary>
    public bool EnableSpaAutoUpdate { get; set; } = true;

    /// <summary>Gets or sets the currently installed SPA version tag.</summary>
    public string CurrentSpaVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the last time an update check was performed (ISO 8601).</summary>
    public string LastSpaUpdateCheck { get; set; } = string.Empty;
}
