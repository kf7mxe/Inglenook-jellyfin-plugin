using System.Security.Claims;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Inglenook.Models;
using Jellyfin.Plugin.Inglenook.Models.Audnexus;
using Jellyfin.Plugin.Inglenook.Models.GoogleBooks;
using Jellyfin.Plugin.Inglenook.Providers.Audnexus;
using Jellyfin.Plugin.Inglenook.Providers.GoogleBooks;
using Jellyfin.Plugin.Inglenook.Providers.OpenLibrary;
using Jellyfin.Plugin.Inglenook.Models.OpenLibrary;
using Jellyfin.Plugin.Inglenook.Providers;
using Jellyfin.Plugin.Inglenook.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Api;

/// <summary>
/// API controller that exposes chapters for audiobook items and bookshelf management.
/// Jellyfin's built-in API only returns chapters for Video items,
/// so this endpoint provides chapter data for Audio/AudioBook/Book items.
/// </summary>
[ApiController]
[Authorize]
[Route("Inglenook")]
public class InglenookController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterRepository _chapterRepository;
    private readonly IProviderManager _providerManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BookshelfService _bookshelfService;
    private readonly AudnexusApiClient _audnexusClient;
    private readonly GoogleBooksApiClient _googleBooksClient;
    private readonly OpenLibraryApiClient _openLibraryClient;
    private readonly ILogger<InglenookController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InglenookController"/> class.
    /// </summary>
    public InglenookController(
        ILibraryManager libraryManager,
        IChapterRepository chapterRepository,
        IProviderManager providerManager,
        IHttpClientFactory httpClientFactory,
        BookshelfService bookshelfService,
        AudnexusApiClient audnexusClient,
        GoogleBooksApiClient googleBooksClient,
        OpenLibraryApiClient openLibraryClient,
        ILogger<InglenookController> logger)
    {
        _libraryManager = libraryManager;
        _chapterRepository = chapterRepository;
        _providerManager = providerManager;
        _httpClientFactory = httpClientFactory;
        _bookshelfService = bookshelfService;
        _audnexusClient = audnexusClient;
        _googleBooksClient = googleBooksClient;
        _openLibraryClient = openLibraryClient;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("id")
                 ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(claim))
        {
            _logger.LogWarning("User ID claim not found. Claims present: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            return Guid.Empty;
        }

        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// Gets available Jellyfin libraries with their IDs and names.
    /// Used by the plugin configuration page to populate library selection.
    /// </summary>
    /// <returns>A list of library objects with Id, Name, and CollectionType.</returns>
    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<object>> GetLibraries()
    {
        var folders = _libraryManager.GetVirtualFolders();
        var result = folders.Select(f => new
        {
            f.ItemId,
            f.Name,
            CollectionType = f.CollectionType?.ToString() ?? string.Empty
        });
        return Ok(result);
    }

    /// <summary>
    /// Gets chapters for an item by its ID.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns>A list of chapter info objects.</returns>
    [HttpGet("{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<ChapterInfo>> GetChapters([FromRoute] Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        var chapters = _chapterRepository.GetChapters(itemId);
        return Ok(chapters);
    }

    /// <summary>
    /// Gets all bookshelves for the current user.
    /// </summary>
    [HttpGet("Bookshelves")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookshelfDto>>> GetBookshelves()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var bookshelves = await _bookshelfService.GetAllAsync(userId).ConfigureAwait(false);
        return Ok(bookshelves);
    }

    /// <summary>
    /// Creates a new bookshelf for the current user.
    /// </summary>
    [HttpPost("Bookshelves")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookshelfDto>> CreateBookshelf([FromBody] CreateBookshelfRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var bookshelf = await _bookshelfService.CreateAsync(userId, request).ConfigureAwait(false);
        return Created($"Inglenook/Bookshelves/{bookshelf.Id}", bookshelf);
    }

    /// <summary>
    /// Updates an existing bookshelf for the current user.
    /// </summary>
    [HttpPut("Bookshelves/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookshelfDto>> UpdateBookshelf([FromRoute] Guid id, [FromBody] UpdateBookshelfRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var bookshelf = await _bookshelfService.UpdateAsync(userId, id, request).ConfigureAwait(false);
        if (bookshelf is null)
        {
            return NotFound();
        }

        return Ok(bookshelf);
    }

    /// <summary>
    /// Deletes a bookshelf for the current user.
    /// </summary>
    [HttpDelete("Bookshelves/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteBookshelf([FromRoute] Guid id)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var deleted = await _bookshelfService.DeleteAsync(userId, id).ConfigureAwait(false);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    // --- Remote Search / Identify ---

    /// <summary>
    /// Searches remote metadata providers (Audnexus, Google Books) for book metadata.
    /// Audnexus only supports direct ASIN lookup (no free-text search), so free-text
    /// queries are handled by Google Books. Author-name queries also search Audnexus
    /// for matching authors and fetch their book details.
    /// </summary>
    [HttpPost("Search/Remote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RemoteSearchResponse>> SearchRemote(
        [FromBody] RemoteSearchRequest request,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var results = new List<RemoteSearchResult>();
        var googleBooksRateLimited = false;
        var query = request.Query?.Trim().Trim('"');

        // If an ItemId is provided, try to use existing provider IDs for direct lookup
        string? existingAsin = null;
        string? existingIsbn = null;
        string? existingGoogleBooksId = null;
        if (request.ItemId.HasValue)
        {
            var item = _libraryManager.GetItemById(request.ItemId.Value);
            if (item != null)
            {
                existingAsin = item.GetProviderId("asin") ?? item.GetProviderId("audible");
                existingIsbn = item.GetProviderId("isbn") ?? item.GetProviderId("isbn13");
                existingGoogleBooksId = item.GetProviderId("googlebooks");

                // Use item name as fallback query
                if (string.IsNullOrWhiteSpace(query))
                {
                    query = item.Name;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(query) && existingAsin == null && existingGoogleBooksId == null)
        {
            return Ok(results);
        }

        var useAudnexus = (config?.EnableAudnexus ?? true)
            && (string.IsNullOrEmpty(request.Provider) || string.Equals(request.Provider, "audnexus", StringComparison.OrdinalIgnoreCase));
        var useGoogleBooks = (config?.EnableGoogleBooks ?? true)
            && (string.IsNullOrEmpty(request.Provider) || string.Equals(request.Provider, "googlebooks", StringComparison.OrdinalIgnoreCase));
        var useOpenLibrary = (config?.EnableOpenLibrary ?? true)
            && (string.IsNullOrEmpty(request.Provider) || string.Equals(request.Provider, "openlibrary", StringComparison.OrdinalIgnoreCase));

        var region = config?.AudnexusRegion ?? "us";

        // Search providers in parallel
        var tasks = new List<Task>();

        if (useAudnexus)
        {
            tasks.Add(Task.Run(async () =>
            {
                var audnexusResults = new List<RemoteSearchResult>();

                // Direct ASIN lookup if the item already has one
                if (!string.IsNullOrEmpty(existingAsin))
                {
                    var book = await _audnexusClient.GetBookByAsinAsync(existingAsin, region, cancellationToken).ConfigureAwait(false);
                    if (book != null)
                    {
                        audnexusResults.Add(MapAudnexusResult(book));
                    }
                }

                // Audnexus only supports author search, not title search.
                // Search for authors matching the query, then fetch book details
                // for the top matching authors (limited to avoid excessive API calls).
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var authors = await _audnexusClient.SearchAuthorsAsync(query, region, cancellationToken).ConfigureAwait(false);

                    // Deduplicate authors by ASIN (API can return duplicates)
                    var uniqueAuthors = authors
                        .GroupBy(a => a.Asin)
                        .Select(g => g.First())
                        .Take(5)
                        .ToList();

                    _logger.LogDebug("Audnexus author search for '{Query}' returned {Count} unique authors", query, uniqueAuthors.Count);

                    // For each author, we don't get books directly from Audnexus.
                    // The author search is useful for identifying author ASINs, but
                    // book discovery relies on Google Books. We include author info
                    // so the UI can show matching authors if needed.
                }

                lock (results)
                {
                    results.AddRange(audnexusResults);
                }
            }, cancellationToken));
        }

        if (useGoogleBooks)
        {
            tasks.Add(Task.Run(async () =>
            {
                var googleResults = new List<RemoteSearchResult>();

                // Direct volume lookup first
                if (!string.IsNullOrEmpty(existingGoogleBooksId))
                {
                    var volume = await _googleBooksClient.GetVolumeAsync(existingGoogleBooksId, cancellationToken).ConfigureAwait(false);
                    if (volume?.VolumeInfo != null)
                    {
                        googleResults.Add(MapGoogleBooksResult(volume));
                    }
                }

                // Search by query (try ISBN first, then title/author)
                var searchQuery = !string.IsNullOrEmpty(existingIsbn) ? $"isbn:{existingIsbn}" : query;
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    var (searchResponse, rateLimited) = await _googleBooksClient.SearchAsync(searchQuery!, cancellationToken).ConfigureAwait(false);
                    if (rateLimited)
                    {
                        googleBooksRateLimited = true;
                    }

                    if (searchResponse?.Items != null)
                    {
                        foreach (var volume in searchResponse.Items)
                        {
                            if (volume.VolumeInfo == null || googleResults.Exists(r => r.ProviderId == volume.Id))
                            {
                                continue;
                            }

                            googleResults.Add(MapGoogleBooksResult(volume));
                        }
                    }
                }

                lock (results)
                {
                    results.AddRange(googleResults);
                }
            }, cancellationToken));
        }

        if (useOpenLibrary)
        {
            tasks.Add(Task.Run(async () =>
            {
                var openLibraryResults = new List<RemoteSearchResult>();

                // Search by ISBN first if available, then by query
                var searchQuery = !string.IsNullOrEmpty(existingIsbn) ? null : query;
                OpenLibrarySearchResponse? searchResponse = null;

                if (!string.IsNullOrEmpty(existingIsbn))
                {
                    searchResponse = await _openLibraryClient.SearchByIsbnAsync(existingIsbn!, cancellationToken).ConfigureAwait(false);
                }

                if ((searchResponse == null || searchResponse.NumFound == 0) && !string.IsNullOrWhiteSpace(searchQuery))
                {
                    searchResponse = await _openLibraryClient.SearchAsync(searchQuery!, cancellationToken).ConfigureAwait(false);
                }

                if (searchResponse?.Docs != null)
                {
                    foreach (var doc in searchResponse.Docs)
                    {
                        if (string.IsNullOrEmpty(doc.Title))
                        {
                            continue;
                        }

                        openLibraryResults.Add(MapOpenLibraryResult(doc));
                    }
                }

                lock (results)
                {
                    results.AddRange(openLibraryResults);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var response = new RemoteSearchResponse
        {
            Results = results
        };

        if (googleBooksRateLimited && results.Count == 0)
        {
            response.Warning = "Google Books rate limit exceeded. Try again in a minute, or add a Google Books API key in the plugin settings for reliable search.";
        }

        return Ok(response);
    }

    /// <summary>
    /// Applies metadata from a remote provider to a Jellyfin item.
    /// </summary>
    [HttpPost("{itemId}/Metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ApplyMetadata(
        [FromRoute] Guid itemId,
        [FromBody] ApplyMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        var config = Plugin.Instance?.Configuration;
        var replace = request.ReplaceExisting;

        if (string.Equals(request.Provider, "audnexus", StringComparison.OrdinalIgnoreCase))
        {
            var region = config?.AudnexusRegion ?? "us";
            var book = await _audnexusClient.GetBookByAsinAsync(request.ProviderId, region, cancellationToken).ConfigureAwait(false);
            if (book == null)
            {
                return BadRequest("Could not fetch metadata from Audnexus");
            }

            await ApplyAudnexusMetadata(item, book, replace, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(request.Provider, "googlebooks", StringComparison.OrdinalIgnoreCase))
        {
            var volume = await _googleBooksClient.GetVolumeAsync(request.ProviderId, cancellationToken).ConfigureAwait(false);
            if (volume?.VolumeInfo == null)
            {
                return BadRequest("Could not fetch metadata from Google Books");
            }

            await ApplyGoogleBooksMetadata(item, volume, replace, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(request.Provider, "openlibrary", StringComparison.OrdinalIgnoreCase))
        {
            // For Open Library, the ProviderId is the work key (e.g., "/works/OL18191919W").
            // Re-search by the work's ISBN to get full metadata, since we stored the search result.
            // The search result already has all the fields we need, so we apply from what's cached
            // in the request. For a fresh lookup, search by the work key's ISBNs.
            var isbn = item.GetProviderId("isbn");
            OpenLibrarySearchResponse? searchResponse = null;

            if (!string.IsNullOrEmpty(isbn))
            {
                searchResponse = await _openLibraryClient.SearchByIsbnAsync(isbn, cancellationToken).ConfigureAwait(false);
            }

            if (searchResponse == null || searchResponse.NumFound == 0)
            {
                // Fall back to searching by title
                searchResponse = await _openLibraryClient.SearchAsync(item.Name, cancellationToken).ConfigureAwait(false);
            }

            var doc = searchResponse?.Docs?.FirstOrDefault(d => d.Key == request.ProviderId)
                ?? searchResponse?.Docs?.FirstOrDefault();

            if (doc == null)
            {
                return BadRequest("Could not fetch metadata from Open Library");
            }

            await ApplyOpenLibraryMetadata(item, doc, replace, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return BadRequest($"Unknown provider: {request.Provider}");
        }

        var parent = item.GetParent();
        await _libraryManager.UpdateItemAsync(item, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Applied {Provider} metadata to item {ItemName} ({ItemId})", request.Provider, item.Name, itemId);

        return Ok(new { Success = true, ItemName = item.Name });
    }

    /// <summary>
    /// Updates the series information for a specific item.
    /// </summary>
    [HttpPost("{itemId}/Series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateSeries(
        [FromRoute] Guid itemId,
        [FromBody] UpdateSeriesRequest request,
        CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        if (item is Book book)
        {
            book.SeriesName = request.SeriesName;
        }
        else if (item is AudioBook audioBook)
        {
            audioBook.SeriesName = request.SeriesName;
        }

        item.IndexNumber = (int?)request.SeriesIndex;

        // Add to ProviderIds as a fallback since SeriesName isn't always mapped in Jellyfin DTOs for all types
        if (!string.IsNullOrEmpty(request.SeriesName))
        {
            item.SetProviderId("SeriesName", request.SeriesName);
        }
        else
        {
            item.ProviderIds.Remove("SeriesName");
        }

        if (request.SeriesIndex.HasValue)
        {
            item.SetProviderId("SeriesIndex", request.SeriesIndex.Value.ToString());
        }
        else
        {
            item.ProviderIds.Remove("SeriesIndex");
        }

        var parent = item.GetParent();
        await _libraryManager.UpdateItemAsync(item, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated series for item {ItemName} to {SeriesName} (Index: {SeriesIndex})", 
            item.Name, request.SeriesName, request.SeriesIndex);

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Gets metadata for a specific item, including provider IDs and people.
    /// </summary>
    [HttpGet("{itemId}/Metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetItemMetadata([FromRoute] Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        string? seriesName = null;
        if (item is Book book)
        {
            seriesName = book.SeriesName;
        }
        else if (item is AudioBook audioBook)
        {
            seriesName = audioBook.SeriesName;
        }

        return Ok(new
        {
            item.Name,
            item.Overview,
            item.ProductionYear,
            item.CommunityRating,
            item.Genres,
            item.Tags,
            SeriesName = seriesName,
            SeriesIndex = item.IndexNumber,
            ProviderIds = item.ProviderIds,
            People = _libraryManager.GetPeople(item).Select(p => new
            {
                p.Name,
                Type = p.Type.ToString(),
                p.Role
            })
        });
    }

    // --- Mapping helpers ---

    private static RemoteSearchResult MapAudnexusResult(AudnexusBook book)
    {
        return new RemoteSearchResult
        {
            Provider = "audnexus",
            ProviderId = book.Asin,
            Title = book.Title,
            Authors = book.Authors.Select(a => a.Name).ToList(),
            Narrators = book.Narrators.Select(n => n.Name).ToList(),
            Description = RemoteProviderHelper.StripHtml(book.Summary ?? book.Description),
            ImageUrl = book.Image,
            Year = book.Copyright ?? RemoteProviderHelper.ParseYear(book.ReleaseDate),
            Publisher = book.PublisherName,
            SeriesName = book.SeriesPrimary?.Name,
            SeriesPosition = book.SeriesPrimary?.Position,
            Rating = float.TryParse(book.Rating, out var r) ? r : null,
            Genres = book.Genres.Where(g => g.Type == "genre").Select(g => g.Name).ToList(),
            Tags = book.Genres.Where(g => g.Type == "tag").Select(g => g.Name).ToList(),
            Isbn = book.Isbn,
            Asin = book.Asin,
            Language = book.Language,
            RuntimeMinutes = book.RuntimeLengthMin
        };
    }

    private static RemoteSearchResult MapGoogleBooksResult(GoogleBooksVolume volume)
    {
        var info = volume.VolumeInfo!;
        var isbn13 = info.IndustryIdentifiers?.FirstOrDefault(i => i.Type == "ISBN_13")?.Identifier;
        var isbn10 = info.IndustryIdentifiers?.FirstOrDefault(i => i.Type == "ISBN_10")?.Identifier;

        return new RemoteSearchResult
        {
            Provider = "googlebooks",
            ProviderId = volume.Id,
            Title = info.Title ?? string.Empty,
            Authors = info.Authors ?? new List<string>(),
            Description = info.Description,
            ImageUrl = info.ImageLinks?.BestImage,
            Year = RemoteProviderHelper.ParseYear(info.PublishedDate),
            Publisher = info.Publisher,
            Rating = info.AverageRating,
            Genres = info.Categories ?? new List<string>(),
            Isbn = isbn13 ?? isbn10,
            Language = info.Language
        };
    }

    private async Task ApplyAudnexusMetadata(BaseItem item, AudnexusBook book, bool replace, CancellationToken cancellationToken)
    {
        if (replace || string.IsNullOrEmpty(item.Name))
        {
            item.Name = book.Title;
        }

        var description = RemoteProviderHelper.StripHtml(book.Summary ?? book.Description);
        if ((replace || string.IsNullOrEmpty(item.Overview)) && !string.IsNullOrEmpty(description))
        {
            item.Overview = description;
        }

        var year = book.Copyright ?? RemoteProviderHelper.ParseYear(book.ReleaseDate);
        if (replace || !item.ProductionYear.HasValue)
        {
            if (year.HasValue)
            {
                item.ProductionYear = year;
            }
        }

        // Series
        if (book.SeriesPrimary != null)
        {
            if (item is Book b)
            {
                if (replace || string.IsNullOrEmpty(b.SeriesName))
                {
                    b.SeriesName = book.SeriesPrimary.Name;
                }
            }
            else if (item is AudioBook ab)
            {
                if (replace || string.IsNullOrEmpty(ab.SeriesName))
                {
                    ab.SeriesName = book.SeriesPrimary.Name;
                }
            }

            if (replace || !item.IndexNumber.HasValue)
            {
                if (float.TryParse(book.SeriesPrimary.Position, out var pos))
                {
                    item.IndexNumber = (int)pos;
                    item.SetProviderId("SeriesIndex", ((int)pos).ToString());
                }
            }

            if (!string.IsNullOrEmpty(book.SeriesPrimary.Name))
            {
                item.SetProviderId("SeriesName", book.SeriesPrimary.Name);
            }
        }

        if (float.TryParse(book.Rating, out var rating))
        {
            var scaledRating = rating * 2f; // 5-star to 10-star
            if (replace || !item.CommunityRating.HasValue)
            {
                item.CommunityRating = scaledRating;
            }
        }

        // Provider IDs
        SetProviderIdIfNeeded(item, "asin", book.Asin, replace);
        SetProviderIdIfNeeded(item, "audible", book.Asin, replace);
        if (!string.IsNullOrEmpty(book.Isbn))
        {
            SetProviderIdIfNeeded(item, "isbn", book.Isbn, replace);
        }

        // Genres
        var genres = book.Genres.Where(g => g.Type == "genre").Select(g => g.Name).ToArray();
        if (genres.Length > 0 && (replace || item.Genres is null || item.Genres.Length == 0))
        {
            item.Genres = genres;
        }

        // Tags
        var tags = book.Genres.Where(g => g.Type == "tag").Select(g => g.Name).ToArray();
        if (tags.Length > 0 && (replace || item.Tags is null || item.Tags.Length == 0))
        {
            item.Tags = tags;
        }

        // People
        var people = new List<PersonInfo>();
        foreach (var author in book.Authors)
        {
            people.Add(new PersonInfo { Name = author.Name, Type = PersonKind.Author });
        }
        foreach (var narrator in book.Narrators)
        {
            people.Add(new PersonInfo { Name = narrator.Name, Type = PersonKind.Actor });
        }

        if (people.Count > 0 && (replace || !_libraryManager.GetPeople(item).Any()))
        {
            _libraryManager.UpdatePeople(item, people);
        }

        // Image
        if (!string.IsNullOrEmpty(book.Image) && (replace || !item.HasImage(ImageType.Primary)))
        {
            await SaveRemoteImage(item, book.Image, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplyGoogleBooksMetadata(BaseItem item, GoogleBooksVolume volume, bool replace, CancellationToken cancellationToken)
    {
        var info = volume.VolumeInfo!;

        if ((replace || string.IsNullOrEmpty(item.Name)) && !string.IsNullOrEmpty(info.Title))
        {
            item.Name = info.Title;
        }

        if ((replace || string.IsNullOrEmpty(item.Overview)) && !string.IsNullOrEmpty(info.Description))
        {
            item.Overview = info.Description;
        }

        var year = RemoteProviderHelper.ParseYear(info.PublishedDate);
        if ((replace || !item.ProductionYear.HasValue) && year.HasValue)
        {
            item.ProductionYear = year;
        }

        if ((replace || !item.CommunityRating.HasValue) && info.AverageRating.HasValue)
        {
            item.CommunityRating = info.AverageRating.Value * 2f; // 5-star to 10-star
        }

        // Provider IDs
        SetProviderIdIfNeeded(item, "googlebooks", volume.Id, replace);
        var isbn13 = info.IndustryIdentifiers?.FirstOrDefault(i => i.Type == "ISBN_13")?.Identifier;
        var isbn10 = info.IndustryIdentifiers?.FirstOrDefault(i => i.Type == "ISBN_10")?.Identifier;
        if (!string.IsNullOrEmpty(isbn13))
        {
            SetProviderIdIfNeeded(item, "isbn", isbn13, replace);
        }
        else if (!string.IsNullOrEmpty(isbn10))
        {
            SetProviderIdIfNeeded(item, "isbn", isbn10, replace);
        }

        // Genres / Categories
        if (info.Categories is { Count: > 0 } && (replace || item.Genres is null || item.Genres.Length == 0))
        {
            item.Genres = info.Categories.ToArray();
        }

        // People
        if (info.Authors is { Count: > 0 } && (replace || !_libraryManager.GetPeople(item).Any()))
        {
            var people = info.Authors.Select(a => new PersonInfo { Name = a, Type = PersonKind.Author }).ToList();
            _libraryManager.UpdatePeople(item, people);
        }

        // Image
        var imageUrl = info.ImageLinks?.BestImage;
        if (!string.IsNullOrEmpty(imageUrl) && (replace || !item.HasImage(ImageType.Primary)))
        {
            await SaveRemoteImage(item, imageUrl, cancellationToken).ConfigureAwait(false);
        }
    }

    private static RemoteSearchResult MapOpenLibraryResult(OpenLibraryDoc doc)
    {
        // Filter subjects to reasonable genres (exclude NYT bestseller tags, series tags, etc.)
        var genres = doc.Subject?
            .Where(s => !s.StartsWith("nyt:", StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith("series:", StringComparison.OrdinalIgnoreCase)
                && !s.Contains("fiction,", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList() ?? new List<string>();

        return new RemoteSearchResult
        {
            Provider = "openlibrary",
            ProviderId = doc.Key,
            Title = doc.Title,
            Authors = doc.AuthorName ?? new List<string>(),
            ImageUrl = doc.CoverImageUrl,
            Year = doc.FirstPublishYear,
            Publisher = doc.Publisher?.FirstOrDefault(),
            Genres = genres,
            Isbn = doc.BestIsbn,
            Language = doc.Language?.FirstOrDefault()
        };
    }

    private async Task ApplyOpenLibraryMetadata(BaseItem item, OpenLibraryDoc doc, bool replace, CancellationToken cancellationToken)
    {
        if ((replace || string.IsNullOrEmpty(item.Name)) && !string.IsNullOrEmpty(doc.Title))
        {
            item.Name = doc.Title;
        }

        if ((replace || !item.ProductionYear.HasValue) && doc.FirstPublishYear.HasValue)
        {
            item.ProductionYear = doc.FirstPublishYear;
        }

        // Provider IDs
        SetProviderIdIfNeeded(item, "openlibrary", doc.Key, replace);
        if (!string.IsNullOrEmpty(doc.BestIsbn))
        {
            SetProviderIdIfNeeded(item, "isbn", doc.BestIsbn, replace);
        }

        // Genres from subjects
        var genres = doc.Subject?
            .Where(s => !s.StartsWith("nyt:", StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith("series:", StringComparison.OrdinalIgnoreCase)
                && !s.Contains("fiction,", StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToArray();
        if (genres is { Length: > 0 } && (replace || item.Genres is null || item.Genres.Length == 0))
        {
            item.Genres = genres;
        }

        // People
        if (doc.AuthorName is { Count: > 0 } && (replace || !_libraryManager.GetPeople(item).Any()))
        {
            var people = doc.AuthorName.Select(a => new PersonInfo { Name = a, Type = PersonKind.Author }).ToList();
            _libraryManager.UpdatePeople(item, people);
        }

        // Image
        if (!string.IsNullOrEmpty(doc.CoverImageUrl) && (replace || !item.HasImage(ImageType.Primary)))
        {
            await SaveRemoteImage(item, doc.CoverImageUrl, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SaveRemoteImage(BaseItem item, string url, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Downloading remote image from {Url} for item {ItemName}", url, item.Name);
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await _providerManager.SaveImage(
                item,
                stream,
                contentType ?? "image/jpeg",
                ImageType.Primary,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully saved image for item {ItemName}", item.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving remote image for item {ItemName}", item.Name);
        }
    }

    private static void SetProviderIdIfNeeded(BaseItem item, string key, string? value, bool replace)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (replace || !item.ProviderIds.ContainsKey(key))
        {
            item.SetProviderId(key, value);
        }
    }
}
