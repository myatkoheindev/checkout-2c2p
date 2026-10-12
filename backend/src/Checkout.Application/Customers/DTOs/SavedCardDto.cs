namespace Checkout.Application.Customers.DTOs;

/// <summary>A saved card as the checkout page offers it. Never carries the customer token.</summary>
/// <param name="Id">The card id, sent back as <c>customerCardId</c> at checkout.</param>
/// <param name="MaskedCardNo">For example <c>411111XXXXXX1111</c>.</param>
/// <param name="CreatedAt">When it was saved, in UTC.</param>
public sealed record SavedCardDto(int Id, string MaskedCardNo, DateTime CreatedAt);
