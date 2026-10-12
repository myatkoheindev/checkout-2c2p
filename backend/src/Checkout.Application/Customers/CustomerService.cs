using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Common;
using Checkout.Application.Customers.DTOs;
using Checkout.Domain.Entities;

namespace Checkout.Application.Customers;

/// <summary>
/// Default <see cref="ICustomerService"/>. Reads go cache-aside: Redis first, then the repository
/// on a miss, then back into Redis.
/// </summary>
public sealed class CustomerService(
    ICustomerRepository customerRepository,
    ICustomerCardRepository cardRepository,
    ICacheService cache) : ICustomerService
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken ct = default) =>
        Result<IReadOnlyList<CustomerDto>>.Success(await LoadAllAsync(ct).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SavedCardDto>>> GetSavedCardsAsync(int customerId, CancellationToken ct = default)
    {
        // Existence comes from the cached customer list, so a cache hit costs no database query.
        var all = await LoadAllAsync(ct).ConfigureAwait(false);
        if (!all.Any(c => c.Id == customerId))
        {
            return Result<IReadOnlyList<SavedCardDto>>.Failure($"Customer {customerId} does not exist.", ErrorCodes.CustomerNotFound);
        }

        // 1. Try Redis.
        var key = CacheKeys.CustomerCards(customerId);
        var cached = await cache.GetAsync<IReadOnlyList<SavedCardDto>>(key).ConfigureAwait(false);
        if (cached is not null)
        {
            return Result<IReadOnlyList<SavedCardDto>>.Success(cached);
        }

        // 2. Miss: load from MySQL through the repository.
        var cards = await cardRepository.GetByCustomerAsync(customerId, ct).ConfigureAwait(false);
        IReadOnlyList<SavedCardDto> dtos = cards.Select(ToDto).ToList();

        // 3. Save to Redis for next time.
        await cache.SetAsync(key, dtos, CacheKeys.CardsTtl).ConfigureAwait(false);
        return Result<IReadOnlyList<SavedCardDto>>.Success(dtos);
    }

    private async Task<IReadOnlyList<CustomerDto>> LoadAllAsync(CancellationToken ct)
    {
        // 1. Try Redis.
        var cached = await cache.GetAsync<IReadOnlyList<CustomerDto>>(CacheKeys.Customers).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        // 2. Miss: load from MySQL through the repository.
        var customers = await customerRepository.GetAllAsync(ct).ConfigureAwait(false);
        IReadOnlyList<CustomerDto> dtos = customers.Select(ToDto).ToList();

        // 3. Save to Redis for next time.
        await cache.SetAsync(CacheKeys.Customers, dtos, CacheKeys.CatalogTtl).ConfigureAwait(false);
        return dtos;
    }

    private static CustomerDto ToDto(Customer c) => new(c.Id, c.FirstName, c.LastName, c.Email);

    private static SavedCardDto ToDto(CustomerCard c) => new(c.Id, c.MaskedCardNo, c.CreatedAt);
}
