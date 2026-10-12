using System.Text.Json;
using Checkout.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Checkout.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> over Redis. Values are stored as JSON strings with an expiry.
/// </summary>
/// <remarks>
/// Redis is an optimisation here, not a dependency: if it is unreachable, a read is a miss, the
/// service loads from the database, and the request still succeeds. Concurrent misses may each
/// load once; for seeded lookups that is cheaper than a lock.
/// </remarks>
public sealed partial class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key)
        where T : class
    {
        // While Redis is disconnected, report a miss at once instead of waiting out a timeout.
        if (!redis.IsConnected)
        {
            LogUnavailable(key);
            return null;
        }

        try
        {
            var raw = await redis.GetDatabase().StringGetAsync(key).ConfigureAwait(false);
            return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(raw.ToString(), JsonOptions);
        }
        catch (Exception exception) when (IsRedisFailure(exception) || exception is JsonException)
        {
            LogReadFailed(key, exception);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        if (!redis.IsConnected)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await redis.GetDatabase().StringSetAsync(key, json, ttl).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogWriteFailed(key, exception);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        if (!redis.IsConnected)
        {
            LogRemoveFailed(key, null);
            return;
        }

        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            // The entry expires on its own; a stale list for a few minutes is the worst case.
            LogRemoveFailed(key, exception);
        }
    }

    private static bool IsRedisFailure(Exception exception) =>
        exception is RedisException or TimeoutException or ObjectDisposedException;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis is not connected; loading {Key} from the database.")]
    private partial void LogUnavailable(string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache read failed for {Key}; loading from the database.")]
    private partial void LogReadFailed(string key, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache write failed for {Key}.")]
    private partial void LogWriteFailed(string key, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache remove failed for {Key}.")]
    private partial void LogRemoveFailed(string key, Exception? exception);
}
