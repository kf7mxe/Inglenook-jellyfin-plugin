namespace Jellyfin.Plugin.Inglenook.Models;

/// <summary>
/// Represents a user's bookshelf with a collection of books.
/// </summary>
public class BookshelfDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> BookIds { get; set; } = new();
    public string? CoverImageUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Request body for creating a new bookshelf.
/// </summary>
public class CreateBookshelfRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request body for updating an existing bookshelf.
/// </summary>
public class UpdateBookshelfRequest
{
    public string? Name { get; set; }
    public List<string>? BookIds { get; set; }
    public string? CoverImageUrl { get; set; }
}
