using Checkout.Domain.Enums;

namespace Checkout.Application.Products.DTOs;

/// <summary>A product as the checkout page lists it.</summary>
/// <param name="Id">The product id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Short description.</param>
/// <param name="Price">Price, two decimals.</param>
/// <param name="Currency">ISO 4217 code; always <c>SGD</c>.</param>
/// <param name="PaymentType"><c>OneTime</c> or <c>Recurring</c>.</param>
public sealed record ProductDto(int Id, string Name, string Description, decimal Price, string Currency, PaymentType PaymentType);
