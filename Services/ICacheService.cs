namespace ProductsService.Services;

/// <summary>
/// Redis-backed distributed cache abstraction.
/// </summary>
public interface ICacheService
{
    /// <summary>Retrieves a cached value, or returns <c>default</c> on miss.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Stores a value in the cache with the given TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Removes a single key from the cache.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes all keys matching a glob-style pattern (uses SCAN + DEL).</summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
