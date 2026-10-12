using Checkout.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Checkout.Infrastructure.Caching;

/// <summary>
/// <see cref="IDistributedLock"/> over Redis: <c>SET key token NX PX ttl</c> to take it, and a
/// compare-and-delete script to release it, so a lease that expired and was taken by someone
/// else is never released by its former owner.
/// </summary>
public sealed partial class RedisDistributedLock(IConnectionMultiplexer redis, ILogger<RedisDistributedLock> logger) : IDistributedLock
{
    /// <summary>Deletes the key only if it still holds this owner's token.</summary>
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    /// <inheritdoc />
    public async Task<LockLease> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (!redis.IsConnected)
        {
            return new LockLease(LockStatus.Unavailable);
        }

        var token = Guid.NewGuid().ToString("N");

        try
        {
            var acquired = await redis.GetDatabase().StringSetAsync(resource, token, ttl, When.NotExists).ConfigureAwait(false);
            if (!acquired)
            {
                return new LockLease(LockStatus.Held);
            }
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogAcquireFailed(resource, exception);
            return new LockLease(LockStatus.Unavailable);
        }

        return new LockLease(LockStatus.Acquired, () => ReleaseAsync(resource, token));
    }

    private async ValueTask ReleaseAsync(string resource, string token)
    {
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(ReleaseScript, [resource], [token]).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // The lease expires on its own after its TTL.
            LogReleaseFailed(resource, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not take lock {Resource}; Redis is unavailable.")]
    private partial void LogAcquireFailed(string resource, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not release lock {Resource}; it will expire.")]
    private partial void LogReleaseFailed(string resource, Exception exception);
}
