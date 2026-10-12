namespace Checkout.Application.Abstractions.Caching;

/// <summary>
/// A cache for read-heavy lookups. Implemented in Infrastructure over Redis.
/// </summary>
/// <remarks>
/// Services use it cache-aside: <see cref="GetAsync{T}"/> first; on a miss, load from the
/// repository and <see cref="SetAsync{T}"/>. A cache failure must never fail the request, so a read
/// that fails is reported as a miss and a write that fails is skipped. Never cache transaction
/// status, which must always be read fresh.
/// </remarks>
public interface ICacheService
{
    /// <summary>Reads a cached value.</summary>
    /// <typeparam name="T">The value type; must round-trip through JSON.</typeparam>
    /// <param name="key">The cache key; see <see cref="CacheKeys"/>.</param>
    /// <returns>The value, or <c>null</c> on a miss or when the cache is unreachable.</returns>
    Task<T?> GetAsync<T>(string key)
        where T : class;

    /// <summary>Caches a value until <paramref name="ttl"/> passes. Skipped when the cache is unreachable.</summary>
    /// <typeparam name="T">The value type; must round-trip through JSON.</typeparam>
    /// <param name="key">The cache key; see <see cref="CacheKeys"/>.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="ttl">How long the value stays cached.</param>
    Task SetAsync<T>(string key, T value, TimeSpan ttl);

    /// <summary>Removes a key, so the next read loads fresh data.</summary>
    Task RemoveAsync(string key);
}
