using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AudiobookChapters.Api;

/// <summary>
/// API controller that exposes chapters for audiobook items.
/// Jellyfin's built-in API only returns chapters for Video items,
/// so this endpoint provides chapter data for Audio/AudioBook/Book items.
/// </summary>
[ApiController]
[Authorize]
[Route("AudiobookChapters")]
public class AudiobookChaptersController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterRepository _chapterRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudiobookChaptersController"/> class.
    /// </summary>
    public AudiobookChaptersController(
        ILibraryManager libraryManager,
        IChapterRepository chapterRepository)
    {
        _libraryManager = libraryManager;
        _chapterRepository = chapterRepository;
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
}
