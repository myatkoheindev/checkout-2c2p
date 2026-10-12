namespace Checkout.Application.Abstractions.Caching;

/// <summary>
/// A lock shared by every API instance. Implemented in Infrastructure over Redis
/// (<c>SET key token NX PX ttl</c>, released only by its owner).
/// </summary>
public interface IDistributedLock
{
    /// <summary>Tries once to take the lock.</summary>
    /// <param name="resource">What is being locked, e.g. <c>checkout:lock:&lt;key&gt;</c>.</param>
    /// <param name="ttl">When the lock expires if it is never released.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>
    /// A lease to dispose when done. Check <see cref="LockLease.Status"/>: only
    /// <see cref="LockStatus.Acquired"/> means this caller holds it.
    /// </returns>
    Task<LockLease> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default);
}

/// <summary>The result of a lock attempt.</summary>
public enum LockStatus
{
    /// <summary>This caller holds the lock until the lease is disposed or expires.</summary>
    Acquired,

    /// <summary>Someone else holds it.</summary>
    Held,

    /// <summary>The lock store is unreachable; the caller must rely on another guard.</summary>
    Unavailable,
}

/// <summary>A lock attempt's outcome. Disposing it releases the lock if it was acquired.</summary>
/// <param name="status">The outcome.</param>
/// <param name="release">Releases the lock; <c>null</c> unless acquired.</param>
public sealed class LockLease(LockStatus status, Func<ValueTask>? release = null) : IAsyncDisposable
{
    private Func<ValueTask>? _release = release;

    public LockStatus Status { get; } = status;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        var release = Interlocked.Exchange(ref _release, null);
        return release?.Invoke() ?? ValueTask.CompletedTask;
    }
}
