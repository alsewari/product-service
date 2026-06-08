namespace ProductsService.DTOs;

/// <summary>
/// Query parameters for searching / filtering the product catalogue.
/// </summary>
public class ProductSearchRequest
{
    /// <summary>Free-text search term matched against name and description.</summary>
    public string? SearchTerm { get; set; }

    /// <summary>Filter by category identifier.</summary>
    public string? CategoryId { get; set; }

    /// <summary>Minimum price (inclusive).</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>Maximum price (inclusive).</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page. Defaults to 20.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Field to sort by. Defaults to "createdAt".</summary>
    public string SortBy { get; set; } = "createdAt";

    /// <summary>Sort direction. Defaults to descending.</summary>
    public bool SortDescending { get; set; } = true;
}
