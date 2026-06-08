using Microsoft.Extensions.Logging;
using ProductsService.Domain;
using ProductsService.Repositories;

namespace ProductsService.Services;

/// <summary>
/// Category service – wraps the repository and invalidates the category cache on mutations.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository     _repo;
    private readonly ICacheService           _cache;
    private readonly ILogger<CategoryService> _logger;

    private const string AllCategoriesCacheKey = "categories:all";
    private static readonly TimeSpan CacheTtl  = TimeSpan.FromMinutes(30);

    public CategoryService(
        ICategoryRepository     repo,
        ICacheService           cache,
        ILogger<CategoryService> logger)
    {
        _repo   = repo;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<List<Category>> GetAllAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<List<Category>>(AllCategoriesCacheKey, ct);
        if (cached is not null)
            return cached;

        var categories = await _repo.GetAllAsync(ct);
        await _cache.SetAsync(AllCategoriesCacheKey, categories, CacheTtl, ct);
        return categories;
    }

    public Task<Category?> GetByIdAsync(string id, CancellationToken ct = default)
        => _repo.GetByIdAsync(id, ct);

    public Task<List<Category>> GetByParentIdAsync(string parentId, CancellationToken ct = default)
        => _repo.GetByParentIdAsync(parentId, ct);

    public async Task<Category> CreateAsync(Category category, CancellationToken ct = default)
    {
        var created = await _repo.CreateAsync(category, ct);
        await _cache.RemoveAsync(AllCategoriesCacheKey, ct);
        return created;
    }

    public async Task<Category?> UpdateAsync(string id, Category category, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateAsync(id, category, ct);
        if (updated is not null)
            await _cache.RemoveAsync(AllCategoriesCacheKey, ct);
        return updated;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var deleted = await _repo.DeleteAsync(id, ct);
        if (deleted)
            await _cache.RemoveAsync(AllCategoriesCacheKey, ct);
        return deleted;
    }
}
