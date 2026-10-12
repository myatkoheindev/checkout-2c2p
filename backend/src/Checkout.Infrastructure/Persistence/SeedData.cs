using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Persistence;

/// <summary>
/// The customers and products the checkout page offers. Inserted on startup when missing, by id,
/// so restarting the API never duplicates them and an edited row is left alone.
/// </summary>
internal static class SeedData
{
    /// <summary>The currency every product is priced in.</summary>
    public const string Currency = "SGD";

    /// <summary>New instances on every call, so no entity object is shared between contexts.</summary>
    public static IReadOnlyList<Customer> Customers =>
    [
        new(1, "Wei Ling", "Tan", "weiling.tan@example.com"),
        new(2, "Arjun", "Nair", "arjun.nair@example.com"),
        new(3, "Siti", "Rahman", "siti.rahman@example.com"),
    ];

    /// <summary>New instances on every call, so no entity object is shared between contexts.</summary>
    public static IReadOnlyList<Product> Products =>
    [
        new(1, "Yoga Drop-in Class", "A single 60-minute yoga class at any studio.", 35.00m, Currency, PaymentType.OneTime),
        new(2, "Personal Training Session", "One 45-minute session with a certified trainer.", 90.00m, Currency, PaymentType.OneTime),
        new(3, "Monthly Unlimited Membership", "Unlimited classes, billed every month.", 199.00m, Currency, PaymentType.Recurring),
        new(4, "Annual Studio Membership", "Twelve months of unlimited classes, billed every year.", 1890.00m, Currency, PaymentType.Recurring),
    ];

    /// <summary>Inserts every seed row whose id is not in the database yet.</summary>
    /// <param name="context">The context to seed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the rows are saved.</returns>
    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingCustomerIds = await context.Set<Customer>().Select(c => c.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var existingProductIds = await context.Set<Product>().Select(p => p.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        AddMissing(context, existingCustomerIds, existingProductIds);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The same seed, synchronously, for the synchronous path of <c>dotnet ef database update</c>.
    /// A real synchronous implementation, so nothing blocks on an async call.
    /// </summary>
    /// <param name="context">The context to seed.</param>
    public static void Seed(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingCustomerIds = context.Set<Customer>().Select(c => c.Id).ToList();
        var existingProductIds = context.Set<Product>().Select(p => p.Id).ToList();
        AddMissing(context, existingCustomerIds, existingProductIds);

        context.SaveChanges();
    }

    private static void AddMissing(DbContext context, List<int> existingCustomerIds, List<int> existingProductIds)
    {
        context.Set<Customer>().AddRange(Customers.Where(c => !existingCustomerIds.Contains(c.Id)));
        context.Set<Product>().AddRange(Products.Where(p => !existingProductIds.Contains(p.Id)));

        // EF Core seeds inside the migration transaction. MySQL commits implicitly after DDL, so a
        // savepoint taken here would already be gone when SaveChanges tried to release it.
        context.Database.AutoSavepointsEnabled = false;
    }
}
