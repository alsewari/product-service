using ProductsService.DTOs;

namespace ProductsService.Services;

/// <summary>
/// Business logic for managing products with cache-aside pattern.
/// </summary>
public interface IProductService
{
    Task<List<ProductResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ProductResponse?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<PagedResult<ProductResponse>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, string sellerId, CancellationToken ct = default);
    Task<ProductResponse?> UpdateAsync(string id, UpdateProductRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
