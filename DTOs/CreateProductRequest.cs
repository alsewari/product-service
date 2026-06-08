using System.ComponentModel.DataAnnotations;

namespace ProductsService.DTOs;

/// <summary>
/// Request body for creating a new product.
/// </summary>
public class CreateProductRequest
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 999999, ErrorMessage = "Price must be between 0.01 and 999999.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "CategoryId is required.")]
    public string CategoryId { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = [];
}
