namespace Checkout.Application.Checkouts.DTOs;

/// <summary>The body of <c>POST /api/checkout</c>. Validated by <c>CheckoutCommandValidator</c>.</summary>
/// <param name="CustomerId">A seeded customer's id.</param>
/// <param name="ProductId">A seeded product's id.</param>
/// <param name="CustomerCardId">One of the customer's saved cards, or omitted to pay with a new payment method.</param>
public sealed record CheckoutCommand(int CustomerId, int ProductId, int? CustomerCardId = null);
