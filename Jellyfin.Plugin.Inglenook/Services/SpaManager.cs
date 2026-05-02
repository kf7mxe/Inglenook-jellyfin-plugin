using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using Jellyfin.Plugin.Inglenook.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Services;

/// <summary>
/// Manages downloading, extracting, and serving the Inglenook SPA from GitHub releases.
/// </summary>
public class SpaManager
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SpaManager> _logger;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".js"] = "application/javascript",
        [".mjs"] = "application/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".eot"] = "application/vnd.ms-fontobject",
        [".map"] = "application/json",
        [".webp"] = "image/webp",
        [".webm"] = "video/webm",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".txt"] = "text/plain",
        [".xml"] = "application/xml",
        [".wasm"] = "application/wasm",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaManager"/> class.
    /// </summary>
    public SpaManager(
        IApplicationPaths applicationPaths,
        IHttpClientFactory httpClientFactory,
        ILogger<SpaManager> logger)
    {
        _applicationPaths = applicationPaths;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the root directory where SPA files are stored.
    /// </summary>
    public string SpaWebRoot => Path.Combine(_applicationPaths.DataPath, "inglenook-spa");

    /// <summary>
    /// Gets whether the SPA is available to serve.
    /// </summary>
    public bool IsAvailable => File.Exists(Path.Combine(SpaWebRoot, "index.html"));

    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    public static string GetMimeType(string extension)
    {
        return MimeTypes.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    /// <summary>
    /// Resolves a relative request path to a full file path, with path traversal protection.
    /// Returns null if the path is invalid or attempts traversal.
    /// </summary>
    public string? GetFilePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return Path.Combine(SpaWebRoot, "index.html");
        }

        // Normalize separators and reject obvious traversal
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(SpaWebRoot, normalized));

        // Ensure resolved path is still within the SPA root
        if (!fullPath.StartsWith(SpaWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fullPath;
    }

    /// <summary>
    /// Checks GitHub for the latest release and returns it if newer than the current version.
    /// Returns null if no update is available or on error.
    /// </summary>
    public async Task<GitHubRelease?> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return null;
        }

        var repo = config.SpaGitHubRepo;
        if (string.IsNullOrWhiteSpace(repo))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-Inglenook-Plugin/1.1");

            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            _logger.LogInformation("Checking for SPA updates at {Url}", url);

            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode} when checking for updates", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release is null || string.IsNullOrEmpty(release.TagName))
            {
                _logger.LogWarning("Could not parse GitHub release response");
                return null;
            }

            // Update last check time
            config.LastSpaUpdateCheck = DateTime.UtcNow.ToString("o");
            Plugin.Instance!.SaveConfiguration();

            // Compare versions
            if (string.Equals(release.TagName, config.CurrentSpaVersion, StringComparison.Ordinal)
                && IsAvailable)
            {
                _logger.LogInformation("SPA is up to date at version {Version}", release.TagName);
                return null;
            }

            _logger.LogInformation("SPA update available: {CurrentVersion} -> {NewVersion}",
                config.CurrentSpaVersion, release.TagName);
            return release;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error checking for SPA updates");
            return null;
        }
    }

    /// <summary>
    /// Downloads and extracts a release to the SPA web root.
    /// </summary>
    public async Task<bool> DownloadAndExtractAsync(GitHubRelease release, CancellationToken cancellationToken)
    {
        if (!await _downloadLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("SPA download already in progress, skipping");
            return false;
        }

        try
        {
            // Find a zip asset, or fall back to zipball URL
            var downloadUrl = FindDownloadUrl(release);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                _logger.LogWarning("No downloadable zip found in release {TagName}", release.TagName);
                return false;
            }

            _logger.LogInformation("Downloading SPA from {Url}", downloadUrl);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-Inglenook-Plugin/1.1");

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Download to temp file
            var tempZip = Path.Combine(Path.GetTempPath(), $"inglenook-spa-{Guid.NewGuid()}.zip");
            var tempExtract = Path.Combine(Path.GetTempPath(), $"inglenook-spa-{Guid.NewGuid()}");

            try
            {
                await using (var fileStream = File.Create(tempZip))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("Extracting SPA archive");

                // Extract zip
                ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

                // Find the directory containing index.html
                var spaRoot = FindSpaRoot(tempExtract);
                if (spaRoot is null)
                {
                    _logger.LogError("Could not find index.html in the extracted archive");
                    return false;
                }

                // Atomic swap: delete old, move new into place
                if (Directory.Exists(SpaWebRoot))
                {
                    Directory.Delete(SpaWebRoot, recursive: true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(SpaWebRoot)!);
                CopyDirectory(spaRoot, SpaWebRoot);

                // Write version info
                var versionInfo = JsonSerializer.Serialize(new
                {
                    version = release.TagName,
                    downloadedAt = DateTime.UtcNow.ToString("o")
                });
                await File.WriteAllTextAsync(
                    Path.Combine(SpaWebRoot, "version.json"),
                    versionInfo,
                    cancellationToken).ConfigureAwait(false);

                // Update config
                var config = Plugin.Instance?.Configuration;
                if (config is not null)
                {
                    config.CurrentSpaVersion = release.TagName;
                    Plugin.Instance!.SaveConfiguration();
                }

                _logger.LogInformation("SPA updated to version {Version}", release.TagName);
                return true;
            }
            finally
            {
                // Clean up temp files
                if (File.Exists(tempZip))
                {
                    File.Delete(tempZip);
                }

                if (Directory.Exists(tempExtract))
                {
                    Directory.Delete(tempExtract, recursive: true);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error downloading/extracting SPA");
            return false;
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>
    /// Gets the currently installed SPA version, or null if not installed.
    /// </summary>
    public string? GetCurrentVersion()
    {
        return Plugin.Instance?.Configuration?.CurrentSpaVersion is { Length: > 0 } v ? v : null;
    }

    private static string? FindDownloadUrl(GitHubRelease release)
    {
        // Look for a .zip asset first (prefer "web" or "spa" in the name)
        var zipAsset = release.Assets
            .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a =>
                a.Name.Contains("web", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("spa", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .FirstOrDefault();

        if (zipAsset is not null)
        {
            return zipAsset.BrowserDownloadUrl;
        }

        // Fall back to source code zipball
        return string.IsNullOrEmpty(release.ZipballUrl) ? null : release.ZipballUrl;
    }

    private static string? FindSpaRoot(string extractPath)
    {
        // Check if index.html is directly in the extract path
        if (File.Exists(Path.Combine(extractPath, "index.html")))
        {
            return extractPath;
        }

        // Search subdirectories (GitHub zipballs have a top-level directory)
        foreach (var dir in Directory.EnumerateDirectories(extractPath, "*", SearchOption.AllDirectories))
        {
            if (File.Exists(Path.Combine(dir, "index.html")))
            {
                return dir;
            }
        }

        return null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}
