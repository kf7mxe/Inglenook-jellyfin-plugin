using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Inglenook.Providers.ExternalIds;

/// <summary>
/// External ID for Audible.
/// </summary>
public class AudibleExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Audible";

    /// <inheritdoc />
    public string Key => "audible";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is AudioBook or Book;
}
