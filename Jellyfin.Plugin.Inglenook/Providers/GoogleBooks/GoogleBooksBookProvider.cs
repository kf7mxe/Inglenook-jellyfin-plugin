using Jellyfin.Plugin.Inglenook.Models.GoogleBooks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.GoogleBooks;

/// <summary>
/// Google Books remote metadata provider for Book items.
/// </summary>
public class GoogleBooksBookProvider : IRemoteMetadataProvider<Book, BookInfo>, IHasOrder
{
    private readonly GoogleBooksApiClient _api;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleBooksBookProvider> _logger;

    /// <inheritdoc />
    public string Name => "Google Books";

    /// <inheritdoc />
    public int Order => RemoteProviderHelper.GetProviderOrder("googlebooks");

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleBooksBookProvider"/> class.
    /// </summary>
    public GoogleBooksBookProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleBooksBookProvider> logger,
        ILogger<GoogleBooksApiClient> apiLogger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _api = new GoogleBooksApiClient(httpClientFactory, apiLogger);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(BookInfo searchInfo, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnableGoogleBooks != true)
        {
            return Enumerable.Empty<RemoteSearchResult>();
        }

        // Direct lookup by Google Books ID
        var googleId = searchInfo.GetProviderId("googlebooks");
        if (!string.IsNullOrEmpty(googleId))
        {
            var volume = await _api.GetVolumeAsync(googleId, cancellationToken).ConfigureAwait(false);
            if (volume?.VolumeInfo != null)
            {
                return new[] { MapToSearchResult(volume) };
            }
        }

        // Search by ISBN
        var isbn = searchInfo.GetProviderId("isbn");
        if (!string.IsNullOrEmpty(isbn))
        {
            var (response, _) = await _api.SearchAsync($"isbn:{isbn}", cancellationToken).ConfigureAwait(false);
            if (response?.Items != null && response.Items.Count > 0)
            {
                return response.Items.Select(MapToSearchResult);
            }
        }

        // Search by name
        if (!string.IsNullOrEmpty(searchInfo.Name))
        {
            var query = $"intitle:{searchInfo.Name}";
            var (response, _) = await _api.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            if (response?.Items != null)
            {
                return response.Items.Select(MapToSearchResult);
            }
        }

        return Enumerable.Empty<RemoteSearchResult>();
    }

    /// <inheritdoc />
    public async Task<MetadataResult<Book>> GetMetadata(BookInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Book>();

        if (Plugin.Instance?.Configuration.EnableGoogleBooks != true)
        {
            return result;
        }

        GoogleBooksVolume? volume = null;

        var googleId = info.GetProviderId("googlebooks");
        if (!string.IsNullOrEmpty(googleId))
        {
            volume = await _api.GetVolumeAsync(googleId, cancellationToken).ConfigureAwait(false);
        }

        if (volume == null)
        {
            var isbn = info.GetProviderId("isbn");
            if (!string.IsNullOrEmpty(isbn))
            {
                var (response, _) = await _api.SearchAsync($"isbn:{isbn}", cancellationToken).ConfigureAwait(false);
                volume = response?.Items?.FirstOrDefault();
            }
        }

        if (volume == null && !string.IsNullOrEmpty(info.Name))
        {
            var (response, _) = await _api.SearchAsync($"intitle:{info.Name}", cancellationToken).ConfigureAwait(false);
            volume = response?.Items?.FirstOrDefault();
        }

        if (volume?.VolumeInfo == null)
        {
            return result;
        }

        var vi = volume.VolumeInfo;
        result.HasMetadata = true;
        result.Item = new Book
        {
            Name = vi.Title ?? string.Empty,
            Overview = RemoteProviderHelper.StripHtml(vi.Description),
            ProductionYear = RemoteProviderHelper.ParseYear(vi.PublishedDate),
            ProviderIds = new Dictionary<string, string>()
        };

        result.Item.SetProviderId("googlebooks", volume.Id);

        if (vi.IndustryIdentifiers != null)
        {
            foreach (var id in vi.IndustryIdentifiers)
            {
                if (string.Equals(id.Type, "ISBN_13", StringComparison.OrdinalIgnoreCase))
                {
                    result.Item.SetProviderId("isbn", id.Identifier);
                }
                else if (string.Equals(id.Type, "ISBN_10", StringComparison.OrdinalIgnoreCase)
                    && !result.Item.ProviderIds.ContainsKey("isbn"))
                {
                    result.Item.SetProviderId("isbn", id.Identifier);
                }
            }
        }

        if (vi.Categories is { Count: > 0 })
        {
            result.Item.Genres = vi.Categories.ToArray();
        }

        if (vi.AverageRating.HasValue)
        {
            result.Item.CommunityRating = vi.AverageRating.Value * 2f;
        }

        if (vi.Authors != null)
        {
            foreach (var author in vi.Authors)
            {
                result.AddPerson(new PersonInfo
                {
                    Name = author,
                    Type = PersonKind.Author
                });
            }
        }

        _logger.LogInformation("Google Books matched Book '{Name}' to volume {Id}", vi.Title, volume.Id);
        return result;
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient("GoogleBooks").GetAsync(url, cancellationToken);
    }

    private static RemoteSearchResult MapToSearchResult(GoogleBooksVolume volume)
    {
        var vi = volume.VolumeInfo;
        var result = new RemoteSearchResult
        {
            Name = vi?.Title ?? string.Empty,
            SearchProviderName = "Google Books",
            ImageUrl = vi?.ImageLinks?.BestImage,
            ProductionYear = RemoteProviderHelper.ParseYear(vi?.PublishedDate),
            Overview = RemoteProviderHelper.StripHtml(vi?.Description)
        };

        result.SetProviderId("googlebooks", volume.Id);

        if (vi?.IndustryIdentifiers != null)
        {
            var isbn = vi.IndustryIdentifiers
                .FirstOrDefault(i => string.Equals(i.Type, "ISBN_13", StringComparison.OrdinalIgnoreCase))
                ?? vi.IndustryIdentifiers
                .FirstOrDefault(i => string.Equals(i.Type, "ISBN_10", StringComparison.OrdinalIgnoreCase));

            if (isbn != null)
            {
                result.SetProviderId("isbn", isbn.Identifier);
            }
        }

        return result;
    }
}
