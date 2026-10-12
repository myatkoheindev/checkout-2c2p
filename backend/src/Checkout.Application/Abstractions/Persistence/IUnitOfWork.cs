namespace Checkout.Application.Abstractions.Persistence;

/// <summary>
/// Commits the changes a use case staged through the repositories, in one call. Shares the
/// request's database context with every repository.
/// </summary>
/// <remarks>
/// Two writes deliberately do not wait for it: <see cref="ITransactionRepository.TryAddAsync"/> and
/// <see cref="ICustomerCardRepository.AddIfMissingAsync"/> save at once, because the MySQL unique
/// index they rely on (idempotency key, saved card) is only checked when the row is written.
/// Status changes are single conditional <c>UPDATE</c> statements and need no commit either.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Writes every staged change.</summary>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The number of rows written.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
