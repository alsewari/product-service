namespace ProductsService.DTOs;

/// <summary>
/// Generic paginated result wrapper.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public class PagedResult<T>
{
    /// <summary>Items on the current page.</summary>
    public List<T> Items { get; set; } = [];

    /// <summary>Total number of matching records across all pages.</summary>
    public long TotalCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Maximum items per page requested.</summary>
    public int PageSize { get; set; }

    /// <summary>Total pages available given TotalCount and PageSize.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Whether a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> Create(List<T> items, long totalCount, int page, int pageSize) => new()
    {
        Items      = items,
        TotalCount = totalCount,
        Page       = page,
        PageSize   = pageSize
    };
}
