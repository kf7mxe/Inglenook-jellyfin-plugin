using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Inglenook.Configuration;
using Jellyfin.Plugin.Inglenook.Providers.Audnexus;
using Jellyfin.Plugin.Inglenook.Providers.GoogleBooks;
using Jellyfin.Plugin.Inglenook.Providers.OpenLibrary;
using Jellyfin.Plugin.Inglenook.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Inglenook;

/// <summary>
/// Registers plugin services with the DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<SpaManager>();
        serviceCollection.AddSingleton<BookshelfService>();
        serviceCollection.AddSingleton<AudnexusApiClient>();
        serviceCollection.AddSingleton<GoogleBooksApiClient>();
        serviceCollection.AddSingleton<OpenLibraryApiClient>();
    }
}

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
