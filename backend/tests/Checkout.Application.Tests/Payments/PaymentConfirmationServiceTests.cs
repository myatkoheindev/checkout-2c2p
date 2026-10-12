using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Payments;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Checkout.Application.Tests.Payments;

public sealed class PaymentConfirmationServiceTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<ICustomerCardRepository> _cards = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly PaymentConfirmationService _sut;

    public PaymentConfirmationServiceTests()
    {
        var product = new Product(3, "Monthly Unlimited Membership", "", 199.00m, "SGD", PaymentType.Recurring);
        var pending = PaymentTransaction.StartPending(2, product, null, "INV1", "key", "hash", DateTime.UtcNow);
        _transactions.Setup(t => t.GetByInvoiceNoAsync("INV1", It.IsAny<CancellationToken>())).ReturnsAsync(pending);

        _sut = new PaymentConfirmationService(
            _transactions.Object,
            _cards.Object,
            _cache.Object,
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<PaymentConfirmationService>.Instance);
    }

    private static PaymentResult Result(string respCode, decimal amount = 199.00m, string? customerToken = null) =>
        new("INV1", StatusOf(respCode), amount, "SGD", respCode, "desc", "VI", "411111XXXXXX1111", "T1", customerToken);

    // The gateway maps provider codes to a status; these tests use 2C2P's codes for readability.
    private static PaymentStatus StatusOf(string respCode) => respCode switch
    {
        "0000" => PaymentStatus.Succeeded,
        "2002" => PaymentStatus.Pending,
        _ => PaymentStatus.Failed,
    };

    [Fact]
    public async Task ApplyAsync_WithNoOutcomeYet_LeavesTheTransactionPending()
    {
        var outcome = await _sut.ApplyAsync(Result("2002", amount: 0m));

        outcome.Should().Be(ConfirmationOutcome.NoOutcomeYet);
        _transactions.Verify(t => t.MarkSucceededIfPendingAsync(It.IsAny<long>(), It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactions.Verify(t => t.MarkFailedIfPendingAsync(It.IsAny<string>(), It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_WithTheWrongAmount_NeverMarksPaid()
    {
        var outcome = await _sut.ApplyAsync(Result("0000", amount: 0.01m));

        outcome.Should().Be(ConfirmationOutcome.AmountMismatch);
        _transactions.Verify(t => t.MarkSucceededIfPendingAsync(It.IsAny<long>(), It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_WithASavedCard_StoresItOnceAndClearsTheCachedList()
    {
        _cards.Setup(c => c.AddIfMissingAsync(It.IsAny<CustomerCard>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _transactions.Setup(t => t.MarkSucceededIfPendingAsync(It.IsAny<long>(), It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var outcome = await _sut.ApplyAsync(Result("0000", customerToken: "tok_1"));

        outcome.Should().Be(ConfirmationOutcome.Applied);
        _cards.Verify(c => c.AddIfMissingAsync(
            It.Is<CustomerCard>(card => card.CustomerId == 2 && card.CustomerToken == "tok_1" && card.MaskedCardNo == "411111XXXXXX1111"),
            It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync(CacheKeys.CustomerCards(2)), Times.Once);
        _transactions.Verify(t => t.MarkSucceededIfPendingAsync(
            It.IsAny<long>(),
            It.Is<PaymentOutcome>(o => o.RespCode == "0000" && o.PaymentChannel == "VI" && o.ProviderRef == "T1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_WithASuccessButNoSavedCardToken_RecordsThePaymentAndSavesNoCard()
    {
        // The customer did not tick "save card" on the hosted page, so 2C2P sends no customerToken.
        _transactions.Setup(t => t.MarkSucceededIfPendingAsync(It.IsAny<long>(), It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var outcome = await _sut.ApplyAsync(Result("0000", customerToken: null));

        outcome.Should().Be(ConfirmationOutcome.Applied);
        _transactions.Verify(t => t.MarkSucceededIfPendingAsync(It.IsAny<long>(), It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()), Times.Once);
        _cards.Verify(c => c.AddIfMissingAsync(It.IsAny<CustomerCard>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheTransactionIsAlreadyFinal_ReportsIt()
    {
        _transactions.Setup(t => t.MarkFailedIfPendingAsync("INV1", It.IsAny<PaymentOutcome>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var outcome = await _sut.ApplyAsync(Result("4051"));

        outcome.Should().Be(ConfirmationOutcome.AlreadyFinal);
    }

    [Fact]
    public async Task ApplyAsync_ForAnUnknownInvoice_ChangesNothing()
    {
        var outcome = await _sut.ApplyAsync(Result("0000") with { InvoiceNo = "INV404" });

        outcome.Should().Be(ConfirmationOutcome.UnknownInvoice);
        _cards.VerifyNoOtherCalls();
    }
}
