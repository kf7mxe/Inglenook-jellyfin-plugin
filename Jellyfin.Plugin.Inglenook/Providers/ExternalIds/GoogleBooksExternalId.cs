using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Inglenook.Providers.ExternalIds;

/// <summary>
/// External ID for Google Books.
/// </summary>
public class GoogleBooksExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Google Books";

    /// <inheritdoc />
    public string Key => "googlebooks";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is AudioBook or Book;
}
