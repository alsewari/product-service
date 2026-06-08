using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsService.Domain;
using ProductsService.DTOs;
using ProductsService.Repositories;
using ProductsService.Services;
using Xunit;

namespace ProductsService.Tests;

/// <summary>
/// Unit tests for <see cref="ProductService"/> verifying cache-aside behaviour.
/// </summary>
public class ProductServiceTests
{
    // ── Shared test data ───────────────────────────────────────────────────────
    private static List<ProductResponse> SampleProductResponses() =>
    [
        new ProductResponse
        {
            Id            = "507f1f77bcf86cd799439011",
            Name          = "Test Widget",
            Description   = "A test widget",
            Price         = 9.99m,
            StockQuantity = 100,
            CategoryId    = "507f1f77bcf86cd799439012",
            CategoryName  = "Widgets",
            SellerId      = "seller-1",
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow.AddDays(-1),
            UpdatedAt     = DateTime.UtcNow.AddDays(-1)
        },
        new ProductResponse
        {
            Id            = "507f1f77bcf86cd799439013",
            Name          = "Super Gadget",
            Description   = "A super gadget",
            Price         = 49.99m,
            StockQuantity = 25,
            CategoryId    = "507f1f77bcf86cd799439012",
            CategoryName  = "Widgets",
            SellerId      = "seller-2",
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        }
    ];

    private static Product SampleProduct(string id = "507f1f77bcf86cd799439011") => new()
    {
        Id            = id,
        Name          = "Test Widget",
        Description   = "A test widget",
        Price         = 9.99m,
        StockQuantity = 100,
        CategoryId    = "507f1f77bcf86cd799439012",
        SellerId      = "seller-1",
        IsActive      = true,
        CreatedAt     = DateTime.UtcNow.AddDays(-1),
        UpdatedAt     = DateTime.UtcNow.AddDays(-1)
    };

    // ── Fixtures ───────────────────────────────────────────────────────────────
    private static (
        Mock<IProductRepository>  productRepoMock,
        Mock<ICategoryRepository> categoryRepoMock,
        Mock<ICacheService>       cacheMock,
        ProductService            sut)
    BuildSut()
    {
        var productRepoMock  = new Mock<IProductRepository>();
        var categoryRepoMock = new Mock<ICategoryRepository>();
        var cacheMock        = new Mock<ICacheService>();
        var loggerMock       = new Mock<ILogger<ProductService>>();

        var sut = new ProductService(
            productRepoMock.Object,
            categoryRepoMock.Object,
            cacheMock.Object,
            loggerMock.Object);

        return (productRepoMock, categoryRepoMock, cacheMock, sut);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GetAllAsync Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAllAsync_WhenCacheHit_ReturnsCachedData_WithoutCallingRepository()
    {
        // Arrange
        var (productRepoMock, _, cacheMock, sut) = BuildSut();

        var cachedProducts = SampleProductResponses();
        cacheMock
            .Setup(c => c.GetAsync<List<ProductResponse>>("products:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedProducts);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(cachedProducts);

        // Repository must NOT be called on a cache hit
        productRepoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<List<ProductResponse>>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_WhenCacheMiss_CallsRepositoryAndReturnsData()
    {
        // Arrange
        var (productRepoMock, categoryRepoMock, cacheMock, sut) = BuildSut();

        // Cache miss
        cacheMock
            .Setup(c => c.GetAsync<List<ProductResponse>>("products:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ProductResponse>?)null);

        var dbProducts = new List<Product> { SampleProduct() };
        productRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbProducts);

        categoryRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Test Widget");

        productRepoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WhenCacheMiss_PopulatesCacheAfterDatabaseFetch()
    {
        // Arrange
        var (productRepoMock, categoryRepoMock, cacheMock, sut) = BuildSut();

        cacheMock
            .Setup(c => c.GetAsync<List<ProductResponse>>("products:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ProductResponse>?)null);

        productRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SampleProduct()]);

        categoryRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await sut.GetAllAsync();

        // Assert – cache should be populated with the data from the DB
        cacheMock.Verify(c => c.SetAsync(
            "products:all",
            It.IsAny<List<ProductResponse>>(),
            TimeSpan.FromMinutes(5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UpdateAsync Tests – cache invalidation
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateAsync_WhenProductExists_InvalidatesPerProductCacheAndBulkCaches()
    {
        // Arrange
        var (productRepoMock, categoryRepoMock, cacheMock, sut) = BuildSut();

        const string productId = "507f1f77bcf86cd799439011";
        var existing = SampleProduct(productId);

        productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        productRepoMock
            .Setup(r => r.UpdateAsync(productId, It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        categoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var updateRequest = new UpdateProductRequest { Price = 19.99m };

        // Act
        var result = await sut.UpdateAsync(productId, updateRequest);

        // Assert
        result.Should().NotBeNull();

        // Per-product key must be evicted
        cacheMock.Verify(c => c.RemoveAsync(
            $"products:{productId}",
            It.IsAny<CancellationToken>()), Times.Once);

        // All-products key must be evicted
        cacheMock.Verify(c => c.RemoveAsync(
            "products:all",
            It.IsAny<CancellationToken>()), Times.Once);

        // Search pattern must be evicted
        cacheMock.Verify(c => c.RemoveByPatternAsync(
            "products:search:*",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductNotFound_ReturnsNull_AndDoesNotInvalidateCache()
    {
        // Arrange
        var (productRepoMock, _, cacheMock, sut) = BuildSut();

        const string productId = "507f1f77bcf86cd799439099";

        productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var updateRequest = new UpdateProductRequest { Price = 19.99m };

        // Act
        var result = await sut.UpdateAsync(productId, updateRequest);

        // Assert
        result.Should().BeNull();

        cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        cacheMock.Verify(c => c.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GetByIdAsync Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_WhenCacheHit_ReturnsCachedProduct_WithoutCallingRepository()
    {
        // Arrange
        var (productRepoMock, _, cacheMock, sut) = BuildSut();

        const string productId = "507f1f77bcf86cd799439011";
        var cached = SampleProductResponses()[0];

        cacheMock
            .Setup(c => c.GetAsync<ProductResponse>($"products:{productId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        // Act
        var result = await sut.GetByIdAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        productRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCacheMiss_PopulatesCacheWithTenMinuteTtl()
    {
        // Arrange
        var (productRepoMock, categoryRepoMock, cacheMock, sut) = BuildSut();

        const string productId = "507f1f77bcf86cd799439011";

        cacheMock
            .Setup(c => c.GetAsync<ProductResponse>($"products:{productId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse?)null);

        productRepoMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleProduct(productId));

        categoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        await sut.GetByIdAsync(productId);

        // Assert – must be cached with 10-minute TTL
        cacheMock.Verify(c => c.SetAsync(
            $"products:{productId}",
            It.IsAny<ProductResponse>(),
            TimeSpan.FromMinutes(10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DeleteAsync Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAsync_WhenSuccessful_InvalidatesAllRelatedCacheKeys()
    {
        // Arrange
        var (productRepoMock, _, cacheMock, sut) = BuildSut();

        const string productId = "507f1f77bcf86cd799439011";

        productRepoMock
            .Setup(r => r.DeleteAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var deleted = await sut.DeleteAsync(productId);

        // Assert
        deleted.Should().BeTrue();

        cacheMock.Verify(c => c.RemoveAsync($"products:{productId}", It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveAsync("products:all", It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.RemoveByPatternAsync("products:search:*", It.IsAny<CancellationToken>()), Times.Once);
    }
}
