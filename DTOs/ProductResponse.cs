using ProductsService.Domain;

namespace ProductsService.DTOs;

/// <summary>
/// API response shape for a product.
/// </summary>
public class ProductResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Maps a <see cref="Product"/> domain object to a <see cref="ProductResponse"/> DTO.
    /// </summary>
    /// <param name="product">The source product.</param>
    /// <param name="categoryName">
    ///     Optional resolved category name. Pass <c>null</c> if not available.
    /// </param>
    public static ProductResponse From(Product product, string? categoryName = null) => new()
    {
        Id            = product.Id,
        Name          = product.Name,
        Description   = product.Description,
        Price         = product.Price,
        StockQuantity = product.StockQuantity,
        CategoryId    = product.CategoryId,
        CategoryName  = categoryName,
        SellerId      = product.SellerId,
        ImageUrls     = product.ImageUrls,
        IsActive      = product.IsActive,
        CreatedAt     = product.CreatedAt,
        UpdatedAt     = product.UpdatedAt
    };
}
