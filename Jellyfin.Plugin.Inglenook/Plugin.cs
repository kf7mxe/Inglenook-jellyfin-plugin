using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Inglenook.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Inglenook;

/// <summary>
/// Inglenook plugin for Jellyfin.
/// Extracts chapter and metadata information from sidecar files for audiobooks.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Inglenook";

    /// <inheritdoc />
    public override Guid Id => new("85a30ae6-b3fd-42ed-899b-419c1b0fe10d");

    /// <inheritdoc />
    public override string Description => "Extracts chapter and metadata information from sidecar files (.cue, .opf, .json, .nfo, .ffmetadata, .txt) for audiobooks.";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }
}
