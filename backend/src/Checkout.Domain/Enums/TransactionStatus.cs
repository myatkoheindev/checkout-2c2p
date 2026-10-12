namespace Checkout.Domain.Enums;

/// <summary>
/// Where a payment stands. Only <see cref="Pending"/> can change; the other two are final.
/// </summary>
public enum TransactionStatus
{
    Pending,
    Succeeded,
    Failed,
}
