using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Inglenook.Providers.ExternalIds;

/// <summary>
/// External ID for ISBN.
/// </summary>
public class IsbnExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "ISBN";

    /// <inheritdoc />
    public string Key => "isbn";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is AudioBook or Book;
}
