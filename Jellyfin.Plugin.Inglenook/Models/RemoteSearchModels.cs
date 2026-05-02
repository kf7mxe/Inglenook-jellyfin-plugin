using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Inglenook.Models;

/// <summary>
/// Request to search remote metadata providers.
/// </summary>
public class RemoteSearchRequest
{
    /// <summary>
    /// Gets or sets the search query (title, author, etc.).
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional Jellyfin item ID to use existing provider IDs for lookup.
    /// </summary>
    public Guid? ItemId { get; set; }

    /// <summary>
    /// Gets or sets which provider to search. Null or empty searches all enabled providers.
    /// </summary>
    public string? Provider { get; set; }
}

/// <summary>
/// A single search result from a remote metadata provider.
/// </summary>
public class RemoteSearchResult
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = new();
    public List<string> Narrators { get; set; } = new();
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? Year { get; set; }
    public string? Publisher { get; set; }
    public string? SeriesName { get; set; }
    public string? SeriesPosition { get; set; }
    public float? Rating { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string? Isbn { get; set; }
    public string? Asin { get; set; }
    public string? Language { get; set; }
    public int? RuntimeMinutes { get; set; }
}

/// <summary>
/// Response from the remote search endpoint.
/// </summary>
public class RemoteSearchResponse
{
    public List<RemoteSearchResult> Results { get; set; } = new();

    /// <summary>
    /// Optional warning message (e.g., rate limiting).
    /// </summary>
    public string? Warning { get; set; }
}

/// <summary>
/// Request to apply remote metadata to a Jellyfin item.
/// </summary>
public class ApplyMetadataRequest
{
    /// <summary>
    /// Gets or sets which provider the result came from.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific ID (ASIN for Audnexus, volume ID for Google Books).
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to overwrite existing metadata fields (default: false, only fills empty fields).
    /// </summary>
    public bool ReplaceExisting { get; set; }
}
