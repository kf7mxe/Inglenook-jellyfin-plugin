using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.Audnexus;

/// <summary>
/// Audnexus remote image provider for AudioBook and Book items.
/// </summary>
public class AudnexusImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly AudnexusApiClient _api;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AudnexusImageProvider> _logger;

    /// <inheritdoc />
    public string Name => "Audnexus";

    /// <inheritdoc />
    public int Order => RemoteProviderHelper.GetProviderOrder("audnexus");

    /// <summary>
    /// Initializes a new instance of the <see cref="AudnexusImageProvider"/> class.
    /// </summary>
    public AudnexusImageProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<AudnexusImageProvider> logger,
        ILogger<AudnexusApiClient> apiLogger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _api = new AudnexusApiClient(httpClientFactory, apiLogger);
    }

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is AudioBook or Book;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new[] { ImageType.Primary };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnableAudnexus != true)
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        var asin = item.GetProviderId("asin") ?? item.GetProviderId("audible");
        if (string.IsNullOrEmpty(asin))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        var region = Plugin.Instance.Configuration.AudnexusRegion;
        var book = await _api.GetBookByAsinAsync(asin, region, cancellationToken).ConfigureAwait(false);

        if (book == null || string.IsNullOrEmpty(book.Image))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        return new[]
        {
            new RemoteImageInfo
            {
                Url = book.Image,
                Type = ImageType.Primary,
                ProviderName = Name
            }
        };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient("Audnexus").GetAsync(url, cancellationToken);
    }
}
