namespace Checkout.Application.Abstractions.Caching;

/// <summary>Every cache key and lifetime in one place, so invalidation can find them.</summary>
public static class CacheKeys
{
    /// <summary>The seeded customers. They never change at runtime.</summary>
    public const string Customers = "checkout:customers";

    /// <summary>The seeded products. They never change at runtime.</summary>
    public const string Products = "checkout:products";

    /// <summary>How long the seeded lists stay cached.</summary>
    public static readonly TimeSpan CatalogTtl = TimeSpan.FromMinutes(10);

    /// <summary>How long a customer's saved cards stay cached. Removed when a card is saved.</summary>
    public static readonly TimeSpan CardsTtl = TimeSpan.FromMinutes(5);

    /// <summary>The key for one customer's saved cards.</summary>
    /// <param name="customerId">The customer.</param>
    /// <returns>For example <c>checkout:customers:1:cards</c>.</returns>
    public static string CustomerCards(int customerId) => $"checkout:customers:{customerId}:cards";
}
