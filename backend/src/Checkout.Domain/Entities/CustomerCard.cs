namespace Checkout.Domain.Entities;

/// <summary>
/// A card a customer saved through 2C2P tokenization. Holds the provider's token and a masked
/// number for display — never the card number or CVV.
/// </summary>
public sealed class CustomerCard
{
    /// <summary>For EF Core.</summary>
    private CustomerCard()
    {
    }

    /// <summary>Records a token issued by 2C2P.</summary>
    /// <param name="customerId">The owner.</param>
    /// <param name="customerToken">The <c>customerToken</c> from the backend notification.</param>
    /// <param name="maskedCardNo">The masked number, e.g. <c>411111XXXXXX1111</c>.</param>
    /// <param name="createdAt">When it was saved, in UTC.</param>
    public CustomerCard(int customerId, string customerToken, string maskedCardNo, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerToken);

        CustomerId = customerId;
        CustomerToken = customerToken;
        MaskedCardNo = maskedCardNo;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public int CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public string CustomerToken { get; private set; } = string.Empty;

    public string MaskedCardNo { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }
}
