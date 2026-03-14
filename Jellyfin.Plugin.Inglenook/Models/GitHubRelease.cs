using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Inglenook.Models;

/// <summary>
/// Represents a GitHub release from the API.
/// </summary>
public class GitHubRelease
{
    /// <summary>Gets or sets the release tag name (e.g. "v1.0.0").</summary>
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    /// <summary>Gets or sets the release assets.</summary>
    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();

    /// <summary>Gets or sets the zipball URL (source code zip).</summary>
    [JsonPropertyName("zipball_url")]
    public string ZipballUrl { get; set; } = string.Empty;
}

/// <summary>
/// Represents an asset attached to a GitHub release.
/// </summary>
public class GitHubAsset
{
    /// <summary>Gets or sets the asset file name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser download URL.</summary>
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the content type.</summary>
    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;
}
