using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Inglenook.Models.Audnexus;

/// <summary>
/// Audnexus book metadata response.
/// </summary>
public class AudnexusBook
{
    [JsonPropertyName("asin")]
    public string Asin { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<AudnexusAuthor> Authors { get; set; } = new();

    [JsonPropertyName("narrators")]
    public List<AudnexusNarrator> Narrators { get; set; } = new();

    [JsonPropertyName("publisherName")]
    public string? PublisherName { get; set; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("runtimeLengthMin")]
    public int? RuntimeLengthMin { get; set; }

    [JsonPropertyName("genres")]
    public List<AudnexusGenre> Genres { get; set; } = new();

    [JsonPropertyName("seriesPrimary")]
    public AudnexusSeries? SeriesPrimary { get; set; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("copyright")]
    public int? Copyright { get; set; }

    [JsonPropertyName("formatType")]
    public string? FormatType { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }
}

/// <summary>
/// Audnexus author reference.
/// </summary>
public class AudnexusAuthor
{
    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Audnexus narrator reference.
/// </summary>
public class AudnexusNarrator
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Audnexus genre/tag entry.
/// </summary>
public class AudnexusGenre
{
    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Audnexus author search result (from GET /authors?name=).
/// </summary>
public class AudnexusAuthorSearchResult
{
    [JsonPropertyName("asin")]
    public string Asin { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Audnexus series information.
/// </summary>
public class AudnexusSeries
{
    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }
}
