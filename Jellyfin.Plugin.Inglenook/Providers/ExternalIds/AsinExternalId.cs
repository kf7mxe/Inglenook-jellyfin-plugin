using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Inglenook.Providers.ExternalIds;

/// <summary>
/// External ID for Amazon ASIN.
/// </summary>
public class AsinExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Amazon";

    /// <inheritdoc />
    public string Key => "asin";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is AudioBook or Book;
}
