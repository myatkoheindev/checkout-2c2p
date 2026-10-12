using Checkout.Application.Customers.DTOs;

namespace Checkout.Api.Contracts.Responses;

/// <summary>A saved card as the checkout page offers it. Never carries the customer token.</summary>
/// <param name="Id">The card id, sent back as <c>customerCardId</c> at checkout.</param>
/// <param name="MaskedCardNo">For example <c>411111XXXXXX1111</c>.</param>
/// <param name="CreatedAt">When it was saved, in UTC.</param>
public sealed record SavedCardResponse(int Id, string MaskedCardNo, DateTime CreatedAt)
{
    /// <summary>Maps the use-case result.</summary>
    public static SavedCardResponse From(SavedCardDto card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return new(card.Id, card.MaskedCardNo, card.CreatedAt);
    }
}
