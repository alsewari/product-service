using ProductsService.Domain;
using ProductsService.DTOs;

namespace ProductsService.Repositories;

/// <summary>
/// Abstraction over the MongoDB products collection.
/// </summary>
public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<(List<Product> Items, long TotalCount)> SearchAsync(
        ProductSearchRequest request, CancellationToken ct = default);
    Task<Product> CreateAsync(Product product, CancellationToken ct = default);
    Task<Product?> UpdateAsync(string id, Product product, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<List<Product>> GetBySellerIdAsync(string sellerId, CancellationToken ct = default);
    Task<List<Product>> GetByCategoryIdAsync(string categoryId, CancellationToken ct = default);
}
