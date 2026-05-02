using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.Inglenook.Models.OpenLibrary;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.OpenLibrary;

/// <summary>
/// HTTP client for the Open Library API.
/// Free, no API key required, no aggressive rate limiting.
/// </summary>
public class OpenLibraryApiClient
{
    private const string BaseUrl = "https://openlibrary.org";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenLibraryApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenLibraryApiClient"/> class.
    /// </summary>
    public OpenLibraryApiClient(IHttpClientFactory httpClientFactory, ILogger<OpenLibraryApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Searches for books by query string (title, author, etc.).
    /// </summary>
    public async Task<OpenLibrarySearchResponse?> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var fields = "title,author_name,first_publish_year,publisher,isbn,cover_i,subject,language,number_of_pages_median,key,subtitle";
            var url = $"{BaseUrl}/search.json?q={Uri.EscapeDataString(query)}&limit=10&fields={fields}";

            using var client = CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open Library search returned {StatusCode} for query '{Query}'", response.StatusCode, query);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OpenLibrarySearchResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Open Library search failed for query '{Query}'", query);
            return null;
        }
    }

    /// <summary>
    /// Searches for books by ISBN.
    /// </summary>
    public async Task<OpenLibrarySearchResponse?> SearchByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        try
        {
            var fields = "title,author_name,first_publish_year,publisher,isbn,cover_i,subject,language,number_of_pages_median,key,subtitle";
            var url = $"{BaseUrl}/search.json?isbn={Uri.EscapeDataString(isbn)}&limit=5&fields={fields}";

            using var client = CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open Library ISBN search returned {StatusCode} for ISBN '{Isbn}'", response.StatusCode, isbn);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OpenLibrarySearchResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Open Library ISBN search failed for ISBN '{Isbn}'", isbn);
            return null;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("OpenLibrary");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Inglenook/1.1 Jellyfin-Plugin");
        return client;
    }
}
