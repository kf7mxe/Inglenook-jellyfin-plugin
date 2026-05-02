using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Inglenook.Providers.ExternalIds;

/// <summary>
/// External ID for Open Library.
/// </summary>
public class OpenLibraryExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Open Library";

    /// <inheritdoc />
    public string Key => "openlibrary";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is AudioBook or Book;
}
