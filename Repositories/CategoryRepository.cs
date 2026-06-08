using MongoDB.Bson;
using MongoDB.Driver;
using ProductsService.Domain;

namespace ProductsService.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="ICategoryRepository"/>.
/// </summary>
public class CategoryRepository : ICategoryRepository
{
    private readonly IMongoCollection<Category> _collection;
    private const string CollectionName = "categories";

    public CategoryRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Category>(CollectionName);
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var indexModels = new List<CreateIndexModel<Category>>
        {
            new(Builders<Category>.IndexKeys.Ascending(c => c.Name),
                new CreateIndexOptions { Name = "idx_name", Unique = true }),

            new(Builders<Category>.IndexKeys.Ascending(c => c.ParentCategoryId),
                new CreateIndexOptions { Name = "idx_parentCategoryId", Sparse = true })
        };

        _collection.Indexes.CreateMany(indexModels);
    }

    public async Task<List<Category>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection
            .Find(c => c.IsActive)
            .SortBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Category?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;

        return await _collection
            .Find(c => c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Category>> GetByParentIdAsync(string parentId, CancellationToken ct = default)
    {
        return await _collection
            .Find(c => c.ParentCategoryId == parentId && c.IsActive)
            .SortBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Category> CreateAsync(Category category, CancellationToken ct = default)
    {
        category.CreatedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(category, cancellationToken: ct);
        return category;
    }

    public async Task<Category?> UpdateAsync(string id, Category category, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;

        category.UpdatedAt = DateTime.UtcNow;
        category.Id        = id;

        var result = await _collection.FindOneAndReplaceAsync(
            Builders<Category>.Filter.Eq(c => c.Id, id),
            category,
            new FindOneAndReplaceOptions<Category> { ReturnDocument = ReturnDocument.After },
            ct);

        return result;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return false;

        var result = await _collection.DeleteOneAsync(
            Builders<Category>.Filter.Eq(c => c.Id, id), ct);

        return result.DeletedCount > 0;
    }
}
