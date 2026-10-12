using Checkout.Application.Checkouts.DTOs;
using Checkout.Application.Checkouts.Validators;

namespace Checkout.Application.Tests.Checkouts;

public sealed class CheckoutRequestValidatorTests
{
    private readonly CheckoutCommandValidator _sut = new();

    [Theory]
    [InlineData(1, 1, null)]
    [InlineData(3, 4, 7)]
    public void Validate_WithPositiveIds_Passes(int customerId, int productId, int? cardId) =>
        _sut.Validate(new CheckoutCommand(customerId, productId, cardId)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0, 1, null, "CustomerId")]
    [InlineData(1, -5, null, "ProductId")]
    [InlineData(1, 1, 0, "CustomerCardId")]
    public void Validate_WithANonPositiveId_FailsOnThatField(int customerId, int productId, int? cardId, string field)
    {
        var result = _sut.Validate(new CheckoutCommand(customerId, productId, cardId));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(field);
    }
}
