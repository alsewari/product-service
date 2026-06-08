using ProductsService.Domain;

namespace ProductsService.Services;

/// <summary>
/// Business logic for managing product categories.
/// </summary>
public interface ICategoryService
{
    Task<List<Category>> GetAllAsync(CancellationToken ct = default);
    Task<Category?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<Category>> GetByParentIdAsync(string parentId, CancellationToken ct = default);
    Task<Category> CreateAsync(Category category, CancellationToken ct = default);
    Task<Category?> UpdateAsync(string id, Category category, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
