using System.Collections.Concurrent;
using System.Text.Json;
using Jellyfin.Plugin.Inglenook.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.Services;

/// <summary>
/// Manages per-user bookshelf storage using JSON files.
/// </summary>
public class BookshelfService
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<BookshelfService> _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _userLocks = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="BookshelfService"/> class.
    /// </summary>
    public BookshelfService(
        IApplicationPaths applicationPaths,
        ILogger<BookshelfService> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    private string GetStorageDir() => Path.Combine(_applicationPaths.DataPath, "inglenook-bookshelves");

    private string GetUserFilePath(Guid userId) => Path.Combine(GetStorageDir(), $"{userId}.json");

    private SemaphoreSlim GetUserLock(Guid userId) => _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    private async Task<List<BookshelfDto>> ReadBookshelvesAsync(Guid userId)
    {
        var filePath = GetUserFilePath(userId);
        if (!File.Exists(filePath))
        {
            return new List<BookshelfDto>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<BookshelfDto>>(json, JsonOptions) ?? new List<BookshelfDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading bookshelves for user {UserId}", userId);
            return new List<BookshelfDto>();
        }
    }

    private async Task WriteBookshelvesAsync(Guid userId, List<BookshelfDto> bookshelves)
    {
        var dir = GetStorageDir();
        Directory.CreateDirectory(dir);

        var filePath = GetUserFilePath(userId);
        var json = JsonSerializer.Serialize(bookshelves, JsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all bookshelves for a user.
    /// </summary>
    public async Task<List<BookshelfDto>> GetAllAsync(Guid userId)
    {
        var userLock = GetUserLock(userId);
        await userLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ReadBookshelvesAsync(userId).ConfigureAwait(false);
        }
        finally
        {
            userLock.Release();
        }
    }

    /// <summary>
    /// Gets a single bookshelf by ID for a user.
    /// </summary>
    public async Task<BookshelfDto?> GetByIdAsync(Guid userId, Guid bookshelfId)
    {
        var bookshelves = await GetAllAsync(userId).ConfigureAwait(false);
        return bookshelves.Find(b => b.Id == bookshelfId);
    }

    /// <summary>
    /// Creates a new bookshelf for a user.
    /// </summary>
    public async Task<BookshelfDto> CreateAsync(Guid userId, CreateBookshelfRequest request)
    {
        var userLock = GetUserLock(userId);
        await userLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var bookshelves = await ReadBookshelvesAsync(userId).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var bookshelf = new BookshelfDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                BookIds = new List<string>(),
                CreatedAt = now,
                UpdatedAt = now
            };
            bookshelves.Add(bookshelf);
            await WriteBookshelvesAsync(userId, bookshelves).ConfigureAwait(false);
            return bookshelf;
        }
        finally
        {
            userLock.Release();
        }
    }

    /// <summary>
    /// Updates an existing bookshelf for a user.
    /// </summary>
    public async Task<BookshelfDto?> UpdateAsync(Guid userId, Guid bookshelfId, UpdateBookshelfRequest request)
    {
        var userLock = GetUserLock(userId);
        await userLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var bookshelves = await ReadBookshelvesAsync(userId).ConfigureAwait(false);
            var bookshelf = bookshelves.Find(b => b.Id == bookshelfId);
            if (bookshelf is null)
            {
                return null;
            }

            if (request.Name is not null)
            {
                bookshelf.Name = request.Name;
            }

            if (request.BookIds is not null)
            {
                bookshelf.BookIds = request.BookIds;
            }

            if (request.CoverImageUrl is not null)
            {
                bookshelf.CoverImageUrl = request.CoverImageUrl;
            }

            bookshelf.UpdatedAt = DateTimeOffset.UtcNow;
            await WriteBookshelvesAsync(userId, bookshelves).ConfigureAwait(false);
            return bookshelf;
        }
        finally
        {
            userLock.Release();
        }
    }

    /// <summary>
    /// Deletes a bookshelf for a user.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid userId, Guid bookshelfId)
    {
        var userLock = GetUserLock(userId);
        await userLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var bookshelves = await ReadBookshelvesAsync(userId).ConfigureAwait(false);
            var removed = bookshelves.RemoveAll(b => b.Id == bookshelfId);
            if (removed == 0)
            {
                return false;
            }

            await WriteBookshelvesAsync(userId, bookshelves).ConfigureAwait(false);
            return true;
        }
        finally
        {
            userLock.Release();
        }
    }
}
