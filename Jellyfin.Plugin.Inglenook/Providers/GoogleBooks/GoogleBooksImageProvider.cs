using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.GoogleBooks;

/// <summary>
/// Google Books remote image provider for AudioBook and Book items.
/// </summary>
public class GoogleBooksImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly GoogleBooksApiClient _api;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleBooksImageProvider> _logger;

    /// <inheritdoc />
    public string Name => "Google Books";

    /// <inheritdoc />
    public int Order => RemoteProviderHelper.GetProviderOrder("googlebooks");

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleBooksImageProvider"/> class.
    /// </summary>
    public GoogleBooksImageProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleBooksImageProvider> logger,
        ILogger<GoogleBooksApiClient> apiLogger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _api = new GoogleBooksApiClient(httpClientFactory, apiLogger);
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
        if (Plugin.Instance?.Configuration.EnableGoogleBooks != true)
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        var googleId = item.GetProviderId("googlebooks");
        if (string.IsNullOrEmpty(googleId))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        var volume = await _api.GetVolumeAsync(googleId, cancellationToken).ConfigureAwait(false);
        var imageUrl = volume?.VolumeInfo?.ImageLinks?.BestImage;

        if (string.IsNullOrEmpty(imageUrl))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        return new[]
        {
            new RemoteImageInfo
            {
                Url = imageUrl,
                Type = ImageType.Primary,
                ProviderName = Name
            }
        };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient("GoogleBooks").GetAsync(url, cancellationToken);
    }
}
