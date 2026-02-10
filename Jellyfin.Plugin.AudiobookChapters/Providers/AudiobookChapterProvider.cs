using Jellyfin.Plugin.AudiobookChapters.Parsers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1862 // Use StringComparison for string comparisons - Guid.ToString() is culture-invariant

namespace Jellyfin.Plugin.AudiobookChapters.Providers;

/// <summary>
/// Metadata provider for audiobook chapters on Audio items.
/// </summary>
public class AudiobookChapterProvider : ICustomMetadataProvider<MediaBrowser.Controller.Entities.Audio.Audio>, IHasItemChangeMonitor
{
    private static readonly string[] MetadataExtensions =
    {
        ".cue", ".opf", ".nfo", ".json", ".txt", ".ffmetadata", ".ffmeta"
    };

    private readonly ILogger<AudiobookChapterProvider> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterRepository _chapterRepository;
    private readonly MetadataAggregator _aggregator;

    /// <inheritdoc />
    public string Name => "Audiobook Chapters";

    /// <summary>
    /// Initializes a new instance of the <see cref="AudiobookChapterProvider"/> class.
    /// </summary>
    public AudiobookChapterProvider(
        ILogger<AudiobookChapterProvider> logger,
        ILibraryManager libraryManager,
        IChapterRepository chapterRepository,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _chapterRepository = chapterRepository;
        _aggregator = new MetadataAggregator(
            loggerFactory.CreateLogger<MetadataAggregator>(),
            loggerFactory);
    }

    /// <inheritdoc />
    public async Task<ItemUpdateType> FetchAsync(
        MediaBrowser.Controller.Entities.Audio.Audio item,
        MetadataRefreshOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.Path) || !IsLibraryEnabled(_libraryManager, item))
        {
            return ItemUpdateType.None;
        }

        var result = await _aggregator.GetMetadataAsync(item.Path, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return ItemUpdateType.None;
        }

        var updateType = ItemUpdateType.None;

        // Save chapters
        if (result.HasChapters)
        {
            _logger.LogInformation("Saving {Count} chapters for {Item}", result.Chapters.Count, item.Name);
            _chapterRepository.SaveChapters(item.Id, result.Chapters);
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Apply metadata (only if item field is empty)
        updateType |= ApplyMetadata(item, result);

        return updateType;
    }

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        return CheckMetadataFilesChanged(item);
    }

    private static ItemUpdateType ApplyMetadata(BaseItem item, Models.ParsedAudiobookMetadata metadata)
    {
        var updateType = ItemUpdateType.None;

        // Provider IDs
        var providerUpdated = false;
        SetProviderId(item, "isbn", metadata.Isbn, ref providerUpdated);
        SetProviderId(item, "isbn13", metadata.Isbn13, ref providerUpdated);
        SetProviderId(item, "asin", metadata.Asin, ref providerUpdated);
        SetProviderId(item, "audible", metadata.AudibleAsin, ref providerUpdated);
        SetProviderId(item, "goodreads", metadata.GoodreadsId, ref providerUpdated);
        SetProviderId(item, "googlebooks", metadata.GoogleBooksId, ref providerUpdated);
        SetProviderId(item, "openlibrary", metadata.OpenLibraryId, ref providerUpdated);

        foreach (var kvp in metadata.ProviderIds)
        {
            SetProviderId(item, kvp.Key, kvp.Value, ref providerUpdated);
        }

        if (providerUpdated)
        {
            updateType |= ItemUpdateType.MetadataImport;
        }

        // Genres
        if (metadata.Genres.Count > 0 && (item.Genres is null || item.Genres.Length == 0))
        {
            item.Genres = metadata.Genres.ToArray();
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Tags
        if (metadata.Tags.Count > 0 && (item.Tags is null || item.Tags.Length == 0))
        {
            item.Tags = metadata.Tags.ToArray();
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Overview (description)
        if (!string.IsNullOrEmpty(metadata.Description) && string.IsNullOrEmpty(item.Overview))
        {
            item.Overview = metadata.Description;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Production year
        if (metadata.Year.HasValue && !item.ProductionYear.HasValue)
        {
            item.ProductionYear = metadata.Year;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Premiere date
        if (metadata.PublishedDate.HasValue && !item.PremiereDate.HasValue)
        {
            item.PremiereDate = metadata.PublishedDate;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Community rating
        if (metadata.CommunityRating.HasValue && !item.CommunityRating.HasValue)
        {
            item.CommunityRating = metadata.CommunityRating;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        return updateType;
    }

    private static void SetProviderId(BaseItem item, string key, string? value, ref bool updated)
    {
        if (!string.IsNullOrEmpty(value) && !item.ProviderIds.ContainsKey(key))
        {
            item.SetProviderId(key, value);
            updated = true;
        }
    }

    internal static bool IsLibraryEnabled(ILibraryManager libraryManager, BaseItem item)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || config.EnabledLibraryIds.Length == 0)
        {
            return true;
        }

        var folders = libraryManager.GetCollectionFolders(item);
        foreach (var folder in folders)
        {
            var folderId = folder.Id.ToString("N");
            foreach (var enabledId in config.EnabledLibraryIds)
            {
                if (string.Equals(folderId, enabledId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool CheckMetadataFilesChanged(BaseItem item)
    {
        if (string.IsNullOrEmpty(item.Path))
        {
            return false;
        }

        var directory = File.Exists(item.Path)
            ? Path.GetDirectoryName(item.Path)
            : Directory.Exists(item.Path) ? item.Path : null;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (MetadataExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (lastWrite > item.DateLastSaved)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Ignore filesystem errors
        }

        return false;
    }
}

/// <summary>
/// Metadata provider for audiobook chapters on Book items.
/// </summary>
public class BookChapterProvider : ICustomMetadataProvider<Book>, IHasItemChangeMonitor
{
    private readonly ILogger<BookChapterProvider> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterRepository _chapterRepository;
    private readonly MetadataAggregator _aggregator;

    /// <inheritdoc />
    public string Name => "Audiobook Chapters (Books)";

    /// <summary>
    /// Initializes a new instance of the <see cref="BookChapterProvider"/> class.
    /// </summary>
    public BookChapterProvider(
        ILogger<BookChapterProvider> logger,
        ILibraryManager libraryManager,
        IChapterRepository chapterRepository,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _chapterRepository = chapterRepository;
        _aggregator = new MetadataAggregator(
            loggerFactory.CreateLogger<MetadataAggregator>(),
            loggerFactory);
    }

    /// <inheritdoc />
    public async Task<ItemUpdateType> FetchAsync(
        Book item,
        MetadataRefreshOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.Path) || !AudiobookChapterProvider.IsLibraryEnabled(_libraryManager, item))
        {
            return ItemUpdateType.None;
        }

        var result = await _aggregator.GetMetadataAsync(item.Path, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return ItemUpdateType.None;
        }

        var updateType = ItemUpdateType.None;

        if (result.HasChapters)
        {
            _logger.LogInformation("Saving {Count} chapters for {Item}", result.Chapters.Count, item.Name);
            _chapterRepository.SaveChapters(item.Id, result.Chapters);
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Apply common metadata
        if (result.Genres.Count > 0 && (item.Genres is null || item.Genres.Length == 0))
        {
            item.Genres = result.Genres.ToArray();
            updateType |= ItemUpdateType.MetadataEdit;
        }

        if (!string.IsNullOrEmpty(result.Description) && string.IsNullOrEmpty(item.Overview))
        {
            item.Overview = result.Description;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        if (result.Year.HasValue && !item.ProductionYear.HasValue)
        {
            item.ProductionYear = result.Year;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        if (result.CommunityRating.HasValue && !item.CommunityRating.HasValue)
        {
            item.CommunityRating = result.CommunityRating;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Provider IDs
        var providerUpdated = false;
        if (!string.IsNullOrEmpty(result.Isbn) && !item.ProviderIds.ContainsKey("isbn"))
        {
            item.SetProviderId("isbn", result.Isbn);
            providerUpdated = true;
        }

        if (!string.IsNullOrEmpty(result.Isbn13) && !item.ProviderIds.ContainsKey("isbn13"))
        {
            item.SetProviderId("isbn13", result.Isbn13);
            providerUpdated = true;
        }

        if (!string.IsNullOrEmpty(result.Asin) && !item.ProviderIds.ContainsKey("asin"))
        {
            item.SetProviderId("asin", result.Asin);
            providerUpdated = true;
        }

        if (providerUpdated)
        {
            updateType |= ItemUpdateType.MetadataImport;
        }

        return updateType;
    }

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        return AudiobookChapterProvider.CheckMetadataFilesChanged(item);
    }
}

/// <summary>
/// Metadata provider for audiobook chapters on AudioBook items.
/// </summary>
public class AudioBookChapterProvider : ICustomMetadataProvider<AudioBook>, IHasItemChangeMonitor
{
    private readonly ILogger<AudioBookChapterProvider> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterRepository _chapterRepository;
    private readonly MetadataAggregator _aggregator;

    /// <inheritdoc />
    public string Name => "Audiobook Chapters";

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioBookChapterProvider"/> class.
    /// </summary>
    public AudioBookChapterProvider(
        ILogger<AudioBookChapterProvider> logger,
        ILibraryManager libraryManager,
        IChapterRepository chapterRepository,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _chapterRepository = chapterRepository;
        _aggregator = new MetadataAggregator(
            loggerFactory.CreateLogger<MetadataAggregator>(),
            loggerFactory);
    }

    /// <inheritdoc />
    public async Task<ItemUpdateType> FetchAsync(
        AudioBook item,
        MetadataRefreshOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.Path) || !AudiobookChapterProvider.IsLibraryEnabled(_libraryManager, item))
        {
            return ItemUpdateType.None;
        }

        var result = await _aggregator.GetMetadataAsync(item.Path, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return ItemUpdateType.None;
        }

        var updateType = ItemUpdateType.None;

        if (result.HasChapters)
        {
            _logger.LogInformation("Saving {Count} chapters for {Item}", result.Chapters.Count, item.Name);
            _chapterRepository.SaveChapters(item.Id, result.Chapters);
            updateType |= ItemUpdateType.MetadataEdit;
        }

        // Apply metadata (only if item field is empty)
        if (result.Genres.Count > 0 && (item.Genres is null || item.Genres.Length == 0))
        {
            item.Genres = result.Genres.ToArray();
            updateType |= ItemUpdateType.MetadataEdit;
        }

        if (!string.IsNullOrEmpty(result.Description) && string.IsNullOrEmpty(item.Overview))
        {
            item.Overview = result.Description;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        if (result.Year.HasValue && !item.ProductionYear.HasValue)
        {
            item.ProductionYear = result.Year;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        if (result.CommunityRating.HasValue && !item.CommunityRating.HasValue)
        {
            item.CommunityRating = result.CommunityRating;
            updateType |= ItemUpdateType.MetadataEdit;
        }

        var providerUpdated = false;
        if (!string.IsNullOrEmpty(result.Isbn) && !item.ProviderIds.ContainsKey("isbn"))
        {
            item.SetProviderId("isbn", result.Isbn);
            providerUpdated = true;
        }

        if (!string.IsNullOrEmpty(result.Asin) && !item.ProviderIds.ContainsKey("asin"))
        {
            item.SetProviderId("asin", result.Asin);
            providerUpdated = true;
        }

        if (providerUpdated)
        {
            updateType |= ItemUpdateType.MetadataImport;
        }

        return updateType;
    }

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        return AudiobookChapterProvider.CheckMetadataFilesChanged(item);
    }
}
