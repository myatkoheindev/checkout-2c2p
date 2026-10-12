namespace Checkout.Application.Checkouts;

/// <summary>Issues invoice numbers for new transactions.</summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>Creates a new invoice number. Uniqueness is enforced by the database index.</summary>
    /// <returns>For example <c>INV202610090482913374</c>.</returns>
    string NewInvoiceNo();
}
