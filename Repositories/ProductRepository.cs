using MongoDB.Bson;
using MongoDB.Driver;
using ProductsService.Domain;
using ProductsService.DTOs;

namespace ProductsService.Repositories;

/// <summary>
/// MongoDB implementation of <see cref="IProductRepository"/>.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly IMongoCollection<Product> _collection;
    private const string CollectionName = "products";

    public ProductRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Product>(CollectionName);
        EnsureIndexes();
    }

    // ── Indexes ────────────────────────────────────────────────────────────────
    private void EnsureIndexes()
    {
        var indexModels = new List<CreateIndexModel<Product>>
        {
            // Text index for full-text search on name and description
            new(Builders<Product>.IndexKeys.Text(p => p.Name).Text(p => p.Description),
                new CreateIndexOptions { Name = "text_name_description" }),

            // Index on categoryId for category filter queries
            new(Builders<Product>.IndexKeys.Ascending(p => p.CategoryId),
                new CreateIndexOptions { Name = "idx_categoryId" }),

            // Index on sellerId for seller-scoped queries
            new(Builders<Product>.IndexKeys.Ascending(p => p.SellerId),
                new CreateIndexOptions { Name = "idx_sellerId" }),

            // Index on price for range filter queries
            new(Builders<Product>.IndexKeys.Ascending(p => p.Price),
                new CreateIndexOptions { Name = "idx_price" }),

            // Compound index for active products ordered by creation time
            new(Builders<Product>.IndexKeys
                    .Ascending(p => p.IsActive)
                    .Descending(p => p.CreatedAt),
                new CreateIndexOptions { Name = "idx_isActive_createdAt" })
        };

        _collection.Indexes.CreateMany(indexModels);
    }

    // ── Read ───────────────────────────────────────────────────────────────────
    public async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection
            .Find(p => p.IsActive)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Product?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;

        return await _collection
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Product> Items, long TotalCount)> SearchAsync(
        ProductSearchRequest request, CancellationToken ct = default)
    {
        var filterBuilder = Builders<Product>.Filter;
        var filters = new List<FilterDefinition<Product>>
        {
            filterBuilder.Eq(p => p.IsActive, true)
        };

        // Free-text search
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            filters.Add(filterBuilder.Text(request.SearchTerm));
        }

        // Category filter
        if (!string.IsNullOrWhiteSpace(request.CategoryId))
        {
            filters.Add(filterBuilder.Eq(p => p.CategoryId, request.CategoryId));
        }

        // Price range filter
        if (request.MinPrice.HasValue)
        {
            filters.Add(filterBuilder.Gte(p => p.Price, request.MinPrice.Value));
        }
        if (request.MaxPrice.HasValue)
        {
            filters.Add(filterBuilder.Lte(p => p.Price, request.MaxPrice.Value));
        }

        var combinedFilter = filterBuilder.And(filters);

        // Build sort definition
        SortDefinition<Product> sort = request.SortBy.ToLowerInvariant() switch
        {
            "price"     => request.SortDescending
                            ? Builders<Product>.Sort.Descending(p => p.Price)
                            : Builders<Product>.Sort.Ascending(p => p.Price),
            "name"      => request.SortDescending
                            ? Builders<Product>.Sort.Descending(p => p.Name)
                            : Builders<Product>.Sort.Ascending(p => p.Name),
            "updatedat" => request.SortDescending
                            ? Builders<Product>.Sort.Descending(p => p.UpdatedAt)
                            : Builders<Product>.Sort.Ascending(p => p.UpdatedAt),
            _           => request.SortDescending            // default: createdAt
                            ? Builders<Product>.Sort.Descending(p => p.CreatedAt)
                            : Builders<Product>.Sort.Ascending(p => p.CreatedAt)
        };

        var skip = (request.Page - 1) * request.PageSize;

        // Execute count and data fetch in parallel
        var countTask = _collection.CountDocumentsAsync(combinedFilter, cancellationToken: ct);
        var itemsTask = _collection
            .Find(combinedFilter)
            .Sort(sort)
            .Skip(skip)
            .Limit(request.PageSize)
            .ToListAsync(ct);

        await Task.WhenAll(countTask, itemsTask);

        return (itemsTask.Result, countTask.Result);
    }

    public async Task<List<Product>> GetBySellerIdAsync(string sellerId, CancellationToken ct = default)
    {
        return await _collection
            .Find(p => p.SellerId == sellerId)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Product>> GetByCategoryIdAsync(string categoryId, CancellationToken ct = default)
    {
        return await _collection
            .Find(p => p.CategoryId == categoryId && p.IsActive)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    // ── Write ──────────────────────────────────────────────────────────────────
    public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(product, cancellationToken: ct);
        return product;
    }

    public async Task<Product?> UpdateAsync(string id, Product product, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;

        product.UpdatedAt = DateTime.UtcNow;
        product.Id        = id; // ensure the ID is preserved

        var result = await _collection.FindOneAndReplaceAsync(
            Builders<Product>.Filter.Eq(p => p.Id, id),
            product,
            new FindOneAndReplaceOptions<Product> { ReturnDocument = ReturnDocument.After },
            ct);

        return result;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return false;

        var result = await _collection.DeleteOneAsync(
            Builders<Product>.Filter.Eq(p => p.Id, id), ct);

        return result.DeletedCount > 0;
    }
}
