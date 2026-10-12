using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Enums;

namespace Checkout.Application.Tests.Abstractions.Payments;

public sealed class PaymentSessionRequestTests
{
    private static PaymentSessionRequest Build(PaymentType type, string? savedCardToken = null, string description = "Yoga Drop-in Class") =>
        PaymentSessionRequest.ForCheckout(
            type,
            "INV202610090000000001",
            description,
            35.00m,
            "SGD",
            "http://localhost:3000/payment/return?invoiceNo=INV202610090000000001",
            "https://example.ngrok-free.app/api/payments/2c2p/notification",
            savedCardToken);

    [Fact]
    public void OneTime_with_a_new_method_offers_every_method()
    {
        var request = Build(PaymentType.OneTime);

        request.CardOnly.Should().BeFalse("every method enabled for the merchant must show");
        request.OfferToSaveCard.Should().BeFalse();
        request.SavedCardToken.Should().BeNull();
    }

    [Fact]
    public void Recurring_with_a_new_method_is_card_only_and_offers_to_save_the_card()
    {
        var request = Build(PaymentType.Recurring);

        request.CardOnly.Should().BeTrue();
        request.OfferToSaveCard.Should().BeTrue();
        request.SavedCardToken.Should().BeNull();
    }

    [Theory]
    [InlineData(PaymentType.OneTime)]
    [InlineData(PaymentType.Recurring)]
    public void A_saved_card_pays_with_that_card_only(PaymentType type)
    {
        var request = Build(type, savedCardToken: "tok_123");

        request.CardOnly.Should().BeTrue();
        request.SavedCardToken.Should().Be("tok_123");
        request.OfferToSaveCard.Should().BeFalse("the card is already saved");
    }

    [Fact]
    public void Every_variant_carries_the_common_fields()
    {
        var request = Build(PaymentType.OneTime);

        request.InvoiceNo.Should().Be("INV202610090000000001");
        request.Amount.Should().Be(35.00m);
        request.Currency.Should().Be("SGD");
        request.ReturnUrl.Should().NotBeNullOrWhiteSpace();
        request.NotificationUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_long_description_is_cut_to_the_limit()
    {
        var request = Build(PaymentType.OneTime, description: new string('x', 300));

        request.Description.Should().HaveLength(PaymentSessionRequest.MaxDescriptionLength);
    }
}
