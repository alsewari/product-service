using ProductsService.Domain;

namespace ProductsService.Repositories;

/// <summary>
/// Abstraction over the MongoDB categories collection.
/// </summary>
public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(CancellationToken ct = default);
    Task<Category?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<Category>> GetByParentIdAsync(string parentId, CancellationToken ct = default);
    Task<Category> CreateAsync(Category category, CancellationToken ct = default);
    Task<Category?> UpdateAsync(string id, Category category, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
