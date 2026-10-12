using Checkout.Domain.Enums;

namespace Checkout.Domain.Entities;

/// <summary>Something a customer can buy. Seeded; there is no product management.</summary>
public sealed class Product
{
    /// <summary>For EF Core.</summary>
    private Product()
    {
    }

    /// <summary>Creates a product.</summary>
    /// <param name="id">The fixed seed id.</param>
    /// <param name="name">Display name.</param>
    /// <param name="description">Short description.</param>
    /// <param name="price">Price in <paramref name="currency"/>, two decimals.</param>
    /// <param name="currency">ISO 4217 code, e.g. <c>SGD</c>.</param>
    /// <param name="paymentType">One-time or recurring.</param>
    public Product(int id, string name, string description, decimal price, string currency, PaymentType paymentType)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        Id = id;
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        PaymentType = paymentType;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public PaymentType PaymentType { get; private set; }
}
