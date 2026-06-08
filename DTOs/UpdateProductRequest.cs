using System.ComponentModel.DataAnnotations;

namespace ProductsService.DTOs;

/// <summary>
/// Request body for updating an existing product. All fields are optional.
/// </summary>
public class UpdateProductRequest
{
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string? Name { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; set; }

    [Range(0.01, 999999, ErrorMessage = "Price must be between 0.01 and 999999.")]
    public decimal? Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int? StockQuantity { get; set; }

    public string? CategoryId { get; set; }

    public List<string>? ImageUrls { get; set; }

    public bool? IsActive { get; set; }
}
