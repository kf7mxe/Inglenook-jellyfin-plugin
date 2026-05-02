using Jellyfin.Plugin.Inglenook.Models.Audnexus;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.Audnexus;

/// <summary>
/// Audnexus remote metadata provider for Book items.
/// </summary>
public class AudnexusBookProvider : IRemoteMetadataProvider<Book, BookInfo>, IHasOrder
{
    private readonly AudnexusApiClient _api;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AudnexusBookProvider> _logger;

    /// <inheritdoc />
    public string Name => "Audnexus";

    /// <inheritdoc />
    public int Order => RemoteProviderHelper.GetProviderOrder("audnexus");

    /// <summary>
    /// Initializes a new instance of the <see cref="AudnexusBookProvider"/> class.
    /// </summary>
    public AudnexusBookProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<AudnexusBookProvider> logger,
        ILogger<AudnexusApiClient> apiLogger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _api = new AudnexusApiClient(httpClientFactory, apiLogger);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(BookInfo searchInfo, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnableAudnexus != true)
        {
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var region = Plugin.Instance.Configuration.AudnexusRegion;

        // Direct lookup by ASIN
        var asin = searchInfo.GetProviderId("asin") ?? searchInfo.GetProviderId("audible");
        if (!string.IsNullOrEmpty(asin))
        {
            var book = await _api.GetBookByAsinAsync(asin, region, cancellationToken).ConfigureAwait(false);
            if (book != null)
            {
                return new[] { MapToSearchResult(book) };
            }
        }

        // Audnexus does not support free-text book search — only ASIN lookup
        return Enumerable.Empty<RemoteSearchResult>();
    }

    /// <inheritdoc />
    public async Task<MetadataResult<Book>> GetMetadata(BookInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Book>();

        if (Plugin.Instance?.Configuration.EnableAudnexus != true)
        {
            return result;
        }

        var region = Plugin.Instance.Configuration.AudnexusRegion;

        var asin = info.GetProviderId("asin") ?? info.GetProviderId("audible");
        AudnexusBook? book = null;

        if (!string.IsNullOrEmpty(asin))
        {
            book = await _api.GetBookByAsinAsync(asin, region, cancellationToken).ConfigureAwait(false);
        }

        if (book == null)
        {
            return result;
        }

        result.HasMetadata = true;
        result.Item = new Book
        {
            Name = book.Title,
            Overview = RemoteProviderHelper.StripHtml(book.Summary ?? book.Description),
            ProductionYear = book.Copyright ?? RemoteProviderHelper.ParseYear(book.ReleaseDate),
            ProviderIds = new Dictionary<string, string>()
        };

        if (!string.IsNullOrEmpty(book.Asin))
        {
            result.Item.SetProviderId("asin", book.Asin);
        }

        if (!string.IsNullOrEmpty(book.Isbn))
        {
            result.Item.SetProviderId("isbn", book.Isbn);
        }

        var genres = book.Genres
            .Where(g => string.Equals(g.Type, "genre", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Name)
            .ToArray();
        if (genres.Length > 0)
        {
            result.Item.Genres = genres;
        }

        var tags = book.Genres
            .Where(g => string.Equals(g.Type, "tag", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Name)
            .ToArray();
        if (tags.Length > 0)
        {
            result.Item.Tags = tags;
        }

        if (float.TryParse(book.Rating, System.Globalization.CultureInfo.InvariantCulture, out var rating))
        {
            result.Item.CommunityRating = rating * 2f;
        }

        foreach (var author in book.Authors)
        {
            result.AddPerson(new PersonInfo
            {
                Name = author.Name,
                Type = PersonKind.Author
            });
        }

        foreach (var narrator in book.Narrators)
        {
            result.AddPerson(new PersonInfo
            {
                Name = narrator.Name,
                Type = PersonKind.Actor
            });
        }

        _logger.LogInformation("Audnexus matched Book '{Name}' to ASIN {Asin}", book.Title, book.Asin);
        return result;
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient("Audnexus").GetAsync(url, cancellationToken);
    }

    private static RemoteSearchResult MapToSearchResult(AudnexusBook book)
    {
        var result = new RemoteSearchResult
        {
            Name = book.Title,
            SearchProviderName = "Audnexus",
            ImageUrl = book.Image,
            ProductionYear = book.Copyright ?? RemoteProviderHelper.ParseYear(book.ReleaseDate),
            Overview = RemoteProviderHelper.StripHtml(book.Description)
        };

        if (!string.IsNullOrEmpty(book.Asin))
        {
            result.SetProviderId("asin", book.Asin);
        }

        if (!string.IsNullOrEmpty(book.Isbn))
        {
            result.SetProviderId("isbn", book.Isbn);
        }

        return result;
    }
}
