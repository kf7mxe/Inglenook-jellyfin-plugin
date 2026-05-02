using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.Inglenook.Models.GoogleBooks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.GoogleBooks;

/// <summary>
/// HTTP client for the Google Books API.
/// </summary>
public class GoogleBooksApiClient
{
    private const string BaseUrl = "https://www.googleapis.com/books/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleBooksApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleBooksApiClient"/> class.
    /// </summary>
    public GoogleBooksApiClient(IHttpClientFactory httpClientFactory, ILogger<GoogleBooksApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Searches for books by query string.
    /// Returns the search response, or null on error.
    /// Sets <paramref name="rateLimited"/> to true if the request was throttled (429).
    /// </summary>
    public async Task<(GoogleBooksSearchResponse? Response, bool RateLimited)> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/volumes?q={Uri.EscapeDataString(query)}&maxResults=10";
            url = AppendApiKey(url);

            using var client = CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Google Books rate limit hit for query '{Query}'. Consider adding a Google Books API key in the plugin configuration.", query);
                    return (null, true);
                }

                _logger.LogWarning("Google Books search returned {StatusCode} for query '{Query}'", response.StatusCode, query);
                return (null, false);
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleBooksSearchResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return (result, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Google Books search failed for query '{Query}'", query);
            return (null, false);
        }
    }

    /// <summary>
    /// Gets a specific volume by its Google Books ID.
    /// </summary>
    public async Task<GoogleBooksVolume?> GetVolumeAsync(string volumeId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/volumes/{Uri.EscapeDataString(volumeId)}";
            url = AppendApiKey(url);

            using var client = CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Books returned {StatusCode} for volume {VolumeId}", response.StatusCode, volumeId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleBooksVolume>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch Google Books volume {VolumeId}", volumeId);
            return null;
        }
    }

    private static string AppendApiKey(string url)
    {
        var apiKey = Plugin.Instance?.Configuration.GoogleBooksApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            url += $"&key={Uri.EscapeDataString(apiKey)}";
        }

        return url;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("GoogleBooks");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Inglenook/1.1 Jellyfin-Plugin");
        return client;
    }
}
