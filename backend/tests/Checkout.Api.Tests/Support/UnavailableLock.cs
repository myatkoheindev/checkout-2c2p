using Checkout.Application.Abstractions.Caching;

namespace Checkout.Api.Tests.Support;

/// <summary>A lock store that is always down, as when Redis is unreachable.</summary>
public sealed class UnavailableLock : IDistributedLock
{
    public Task<LockLease> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LockLease(LockStatus.Unavailable));
}
