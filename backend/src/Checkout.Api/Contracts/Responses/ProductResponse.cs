using Checkout.Application.Products.DTOs;
using Checkout.Domain.Enums;

namespace Checkout.Api.Contracts.Responses;

/// <summary>A product as the checkout page lists it.</summary>
/// <param name="Id">The product id.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">A short description.</param>
/// <param name="Price">The price, two decimals.</param>
/// <param name="Currency">ISO 4217 code; always <c>SGD</c> here.</param>
/// <param name="PaymentType">OneTime or Recurring; decides the 2C2P flow.</param>
public sealed record ProductResponse(int Id, string Name, string Description, decimal Price, string Currency, PaymentType PaymentType)
{
    /// <summary>Maps the use-case result.</summary>
    public static ProductResponse From(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new(product.Id, product.Name, product.Description, product.Price, product.Currency, product.PaymentType);
    }
}
