using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductsService.Domain;
using ProductsService.DTOs;
using ProductsService.Repositories;

namespace ProductsService.Services;

/// <summary>
/// Implementation of <see cref="IProductService"/> using the cache-aside pattern backed by Redis.
/// </summary>
public class ProductService : IProductService
{
    private readonly IProductRepository  _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ICacheService       _cache;
    private readonly ILogger<ProductService> _logger;

    // Cache key constants
    private const string AllProductsCacheKey     = "products:all";
    private const string ProductCacheKeyPrefix   = "products:";
    private const string SearchCacheKeyPrefix    = "products:search:";

    // TTLs
    private static readonly TimeSpan AllProductsTtl  = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProductTtl      = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SearchTtl       = TimeSpan.FromMinutes(2);

    public ProductService(
        IProductRepository  productRepo,
        ICategoryRepository categoryRepo,
        ICacheService       cache,
        ILogger<ProductService> logger)
    {
        _productRepo  = productRepo;
        _categoryRepo = categoryRepo;
        _cache        = cache;
        _logger       = logger;
    }

    // ── GetAll ─────────────────────────────────────────────────────────────────
    public async Task<List<ProductResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<List<ProductResponse>>(AllProductsCacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache HIT: {Key}", AllProductsCacheKey);
            return cached;
        }

        _logger.LogDebug("Cache MISS: {Key} – loading from database.", AllProductsCacheKey);

        var products   = await _productRepo.GetAllAsync(ct);
        var categories = await _categoryRepo.GetAllAsync(ct);
        var catMap     = categories.ToDictionary(c => c.Id, c => c.Name);

        var response = products
            .Select(p => ProductResponse.From(p, catMap.GetValueOrDefault(p.CategoryId)))
            .ToList();

        await _cache.SetAsync(AllProductsCacheKey, response, AllProductsTtl, ct);

        return response;
    }

    // ── GetById ────────────────────────────────────────────────────────────────
    public async Task<ProductResponse?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var cacheKey = $"{ProductCacheKeyPrefix}{id}";

        var cached = await _cache.GetAsync<ProductResponse>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache HIT: {Key}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache MISS: {Key} – loading from database.", cacheKey);

        var product = await _productRepo.GetByIdAsync(id, ct);
        if (product is null)
            return null;

        string? categoryName = null;
        if (!string.IsNullOrWhiteSpace(product.CategoryId))
        {
            var category = await _categoryRepo.GetByIdAsync(product.CategoryId, ct);
            categoryName = category?.Name;
        }

        var response = ProductResponse.From(product, categoryName);
        await _cache.SetAsync(cacheKey, response, ProductTtl, ct);

        return response;
    }

    // ── Search ─────────────────────────────────────────────────────────────────
    public async Task<PagedResult<ProductResponse>> SearchAsync(
        ProductSearchRequest request, CancellationToken ct = default)
    {
        var cacheKey = BuildSearchCacheKey(request);

        var cached = await _cache.GetAsync<PagedResult<ProductResponse>>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache HIT: {Key}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache MISS: {Key} – searching database.", cacheKey);

        var (items, totalCount) = await _productRepo.SearchAsync(request, ct);

        // Resolve category names in bulk
        var categoryIds = items.Select(p => p.CategoryId).Distinct().ToList();
        var categoryMap = new Dictionary<string, string>();

        foreach (var catId in categoryIds)
        {
            if (!string.IsNullOrWhiteSpace(catId))
            {
                var cat = await _categoryRepo.GetByIdAsync(catId, ct);
                if (cat is not null)
                    categoryMap[catId] = cat.Name;
            }
        }

        var responseItems = items
            .Select(p => ProductResponse.From(p, categoryMap.GetValueOrDefault(p.CategoryId)))
            .ToList();

        var result = PagedResult<ProductResponse>.Create(responseItems, totalCount, request.Page, request.PageSize);

        await _cache.SetAsync(cacheKey, result, SearchTtl, ct);

        return result;
    }

    // ── Create ─────────────────────────────────────────────────────────────────
    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request, string sellerId, CancellationToken ct = default)
    {
        var product = new Product
        {
            Name          = request.Name,
            Description   = request.Description,
            Price         = request.Price,
            StockQuantity = request.StockQuantity,
            CategoryId    = request.CategoryId,
            SellerId      = sellerId,
            ImageUrls     = request.ImageUrls,
            IsActive      = true
        };

        var created = await _productRepo.CreateAsync(product, ct);

        // Invalidate the all-products cache and any search caches
        await InvalidateProductCachesAsync(ct);

        string? categoryName = null;
        if (!string.IsNullOrWhiteSpace(created.CategoryId))
        {
            var category = await _categoryRepo.GetByIdAsync(created.CategoryId, ct);
            categoryName = category?.Name;
        }

        return ProductResponse.From(created, categoryName);
    }

    // ── Update ─────────────────────────────────────────────────────────────────
    public async Task<ProductResponse?> UpdateAsync(
        string id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var existing = await _productRepo.GetByIdAsync(id, ct);
        if (existing is null)
            return null;

        // Apply only the fields that were provided
        if (request.Name          is not null) existing.Name          = request.Name;
        if (request.Description   is not null) existing.Description   = request.Description;
        if (request.Price         is not null) existing.Price         = request.Price.Value;
        if (request.StockQuantity is not null) existing.StockQuantity = request.StockQuantity.Value;
        if (request.CategoryId    is not null) existing.CategoryId    = request.CategoryId;
        if (request.ImageUrls     is not null) existing.ImageUrls     = request.ImageUrls;
        if (request.IsActive      is not null) existing.IsActive      = request.IsActive.Value;

        var updated = await _productRepo.UpdateAsync(id, existing, ct);
        if (updated is null)
            return null;

        // Invalidate per-product cache and bulk caches
        await _cache.RemoveAsync($"{ProductCacheKeyPrefix}{id}", ct);
        await InvalidateProductCachesAsync(ct);

        string? categoryName = null;
        if (!string.IsNullOrWhiteSpace(updated.CategoryId))
        {
            var category = await _categoryRepo.GetByIdAsync(updated.CategoryId, ct);
            categoryName = category?.Name;
        }

        return ProductResponse.From(updated, categoryName);
    }

    // ── Delete ─────────────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var deleted = await _productRepo.DeleteAsync(id, ct);
        if (deleted)
        {
            await _cache.RemoveAsync($"{ProductCacheKeyPrefix}{id}", ct);
            await InvalidateProductCachesAsync(ct);
        }
        return deleted;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Invalidates the all-products key and all search keys via pattern deletion.
    /// </summary>
    private async Task InvalidateProductCachesAsync(CancellationToken ct)
    {
        await _cache.RemoveAsync(AllProductsCacheKey, ct);
        await _cache.RemoveByPatternAsync($"{SearchCacheKeyPrefix}*", ct);
    }

    /// <summary>
    /// Builds a deterministic cache key from a <see cref="ProductSearchRequest"/>
    /// by hashing its JSON representation with SHA-256.
    /// </summary>
    private static string BuildSearchCacheKey(ProductSearchRequest request)
    {
        var json  = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash  = Convert.ToHexString(bytes)[..16]; // first 16 hex chars is sufficient
        return $"{SearchCacheKeyPrefix}{hash}";
    }
}
