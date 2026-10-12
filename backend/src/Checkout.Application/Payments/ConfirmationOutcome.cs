namespace Checkout.Application.Payments;

/// <summary>What applying a provider result to its transaction did.</summary>
public enum ConfirmationOutcome
{
    /// <summary>The transaction moved to Succeeded or Failed.</summary>
    Applied,

    /// <summary>The transaction was already final; nothing changed (a repeat or a race).</summary>
    AlreadyFinal,

    /// <summary>The provider has no outcome yet; the transaction stays Pending.</summary>
    NoOutcomeYet,

    /// <summary>No transaction has this invoice number.</summary>
    UnknownInvoice,

    /// <summary>A success whose amount or currency differs from what was stored; not applied.</summary>
    AmountMismatch,
}
