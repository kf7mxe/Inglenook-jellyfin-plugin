using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.Inglenook.Models.Audnexus;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Providers.Audnexus;

/// <summary>
/// HTTP client for the Audnexus API.
/// </summary>
public class AudnexusApiClient
{
    private const string BaseUrl = "https://api.audnex.us";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AudnexusApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudnexusApiClient"/> class.
    /// </summary>
    public AudnexusApiClient(IHttpClientFactory httpClientFactory, ILogger<AudnexusApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets audiobook metadata by ASIN.
    /// </summary>
    public async Task<AudnexusBook?> GetBookByAsinAsync(string asin, string region, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/books/{Uri.EscapeDataString(asin)}";
            if (!string.IsNullOrEmpty(region) && !string.Equals(region, "us", StringComparison.OrdinalIgnoreCase))
            {
                url += $"?region={Uri.EscapeDataString(region)}";
            }

            using var client = CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Audnexus returned {StatusCode} for ASIN {Asin}", response.StatusCode, asin);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AudnexusBook>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch book from Audnexus for ASIN {Asin}", asin);
            return null;
        }
    }

    /// <summary>
    /// Searches for authors by name, then fetches book metadata for each matching author's ASIN.
    /// Note: Audnexus only supports author search (GET /authors?name=), not book title search.
    /// </summary>
    public async Task<List<AudnexusAuthorSearchResult>> SearchAuthorsAsync(string query, string region, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{BaseUrl}/authors?name={Uri.EscapeDataString(query)}";
            if (!string.IsNullOrEmpty(region) && !string.Equals(region, "us", StringComparison.OrdinalIgnoreCase))
            {
                url += $"&region={Uri.EscapeDataString(region)}";
            }

            using var client = CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Audnexus author search returned {StatusCode} for query '{Query}'", response.StatusCode, query);
                return new List<AudnexusAuthorSearchResult>();
            }

            var results = await response.Content.ReadFromJsonAsync<List<AudnexusAuthorSearchResult>>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return results ?? new List<AudnexusAuthorSearchResult>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Audnexus author search failed for query '{Query}'", query);
            return new List<AudnexusAuthorSearchResult>();
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("Audnexus");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Inglenook/1.1 Jellyfin-Plugin");
        return client;
    }
}
