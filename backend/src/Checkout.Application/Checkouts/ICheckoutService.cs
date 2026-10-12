using Checkout.Application.Checkouts.DTOs;
using Checkout.Application.Common;

namespace Checkout.Application.Checkouts;

/// <summary>Starts a payment, at most once per idempotency key.</summary>
public interface ICheckoutService
{
    /// <summary>
    /// Creates the transaction and starts the payment with the provider — or, if this idempotency key was
    /// seen before, returns the original result without calling the provider again.
    /// </summary>
    /// <param name="request">The checkout body.</param>
    /// <param name="idempotencyKey">The client's <c>Idempotency-Key</c>: one purchase, however many requests.</param>
    /// <param name="clientIp">The shopper's IP, passed to Do Payment when known.</param>
    /// <param name="ct">Cancels the request.</param>
    Task<Result<CheckoutResult>> CheckoutAsync(CheckoutCommand request, string idempotencyKey, string? clientIp, CancellationToken ct = default);
}
