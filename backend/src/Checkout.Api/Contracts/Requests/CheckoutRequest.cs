using Checkout.Application.Checkouts.DTOs;

namespace Checkout.Api.Contracts.Requests;

/// <summary>The body of <c>POST /api/checkout</c>.</summary>
/// <param name="CustomerId">A seeded customer's id.</param>
/// <param name="ProductId">A seeded product's id.</param>
/// <param name="CustomerCardId">One of the customer's saved cards, or omitted to pay with a new payment method.</param>
public sealed record CheckoutRequest(int CustomerId, int ProductId, int? CustomerCardId = null)
{
    /// <summary>The use-case command for this request.</summary>
    public CheckoutCommand ToCommand() => new(CustomerId, ProductId, CustomerCardId);
}
