using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ProductsService.Services;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/>.
/// Uses <see cref="System.Text.Json"/> for serialisation.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheService>  _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false
    };

    public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
    {
        _redis  = redis;
        _logger = logger;
    }

    // ── Get ────────────────────────────────────────────────────────────────────
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var db    = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key '{Key}'. Falling through to source.", key);
            return default;
        }
    }

    // ── Set ────────────────────────────────────────────────────────────────────
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var db         = _redis.GetDatabase();
            var serialised = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, serialised, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key '{Key}'.", key);
        }
    }

    // ── Remove single ──────────────────────────────────────────────────────────
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key '{Key}'.", key);
        }
    }

    // ── Remove by pattern (SCAN + DEL) ─────────────────────────────────────────
    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        try
        {
            var server   = _redis.GetServer(_redis.GetEndPoints().First());
            var keysEnum = server.KeysAsync(pattern: pattern);

            var keys = new List<RedisKey>();
            await foreach (var key in keysEnum)
            {
                keys.Add(key);
            }

            if (keys.Count > 0)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync([.. keys]);
                _logger.LogDebug("Removed {Count} keys matching pattern '{Pattern}'.", keys.Count, pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE BY PATTERN failed for pattern '{Pattern}'.", pattern);
        }
    }
}
