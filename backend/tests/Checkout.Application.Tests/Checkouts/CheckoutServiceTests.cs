using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Checkouts;
using Checkout.Application.Checkouts.DTOs;
using Checkout.Application.Checkouts.Validators;
using Checkout.Application.Common;
using Checkout.Application.Configuration;
using Checkout.Application.Payments;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Checkout.Application.Tests.Checkouts;

public sealed class CheckoutServiceTests
{
    private const string Key = "3f2c9a1e-6b7d-4c58-9e0a-2d4b8f1c7e63";

    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<ICustomerCardRepository> _cards = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IPaymentConfirmationService> _confirmation = new();
    private readonly Mock<IDistributedLock> _lock = new();
    private readonly Mock<IInvoiceNumberGenerator> _invoices = new();
    private readonly CheckoutService _sut;

    private static readonly Product Yoga = new(1, "Yoga Drop-in Class", "60 minutes", 35.00m, "SGD", PaymentType.OneTime);

    public CheckoutServiceTests() : this(TimeSpan.FromSeconds(10))
    {
    }

    private CheckoutServiceTests(TimeSpan lockWait)
    {
        _products.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Yoga);
        _customers.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _lock.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LockLease(LockStatus.Acquired));

        _sut = new CheckoutService(
            _products.Object,
            _customers.Object,
            _cards.Object,
            _transactions.Object,
            _unitOfWork.Object,
            _gateway.Object,
            _confirmation.Object,
            _lock.Object,
            _invoices.Object,
            new CheckoutCommandValidator(),
            Options.Create(new CheckoutOptions
            {
                PublicApiUrl = "https://api.example.com",
                LockWait = lockWait,
                LockPollInterval = TimeSpan.FromMilliseconds(10),
            }),
            Options.Create(new FrontendOptions { Url = "http://localhost:3000" }),
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<CheckoutService>.Instance);
    }

    private static PaymentTransaction Existing(CheckoutCommand request, string? paymentToken = "pt_1")
    {
        var transaction = PaymentTransaction.StartPending(
            request.CustomerId, Yoga, request.CustomerCardId, "INV1", Key, CheckoutService.HashOf(request), DateTime.UtcNow);

        if (paymentToken is not null)
        {
            transaction.AttachPaymentToken(paymentToken, "https://sandbox-pgw-ui.2c2p.com/x", DateTime.UtcNow);
        }

        return transaction;
    }

    [Fact]
    public async Task CheckoutAsync_ForTheHostedPage_SavesTheSessionThroughTheUnitOfWork()
    {
        var request = new CheckoutCommand(1, 1);
        _invoices.Setup(i => i.NewInvoiceNo()).Returns("INV1");
        _transactions.Setup(t => t.TryAddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransactionInsertResult.Added);
        _gateway.Setup(g => g.CreateSessionAsync(It.IsAny<PaymentSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession(true, "pt_1", "https://sandbox-pgw-ui.2c2p.com/x", "0000", "Success"));
        _transactions.Setup(t => t.GetByInvoiceNoAsync("INV1", It.IsAny<CancellationToken>())).ReturnsAsync(Existing(request));

        var result = await _sut.CheckoutAsync(request, Key, clientIp: null);

        result.IsSuccess.Should().BeTrue();
        // The insert saves itself (unique-index guard); attaching the session is committed once, by the unit of work.
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckoutAsync_WithInvalidIds_ReturnsValidationError_AndTouchesNothing()
    {
        var result = await _sut.CheckoutAsync(new CheckoutCommand(0, -1), Key, clientIp: null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationError);
        result.ValidationErrors.Should().ContainKeys("CustomerId", "ProductId");
        _products.Verify(p => p.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _lock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_WithUnknownProduct_ReturnsProductNotFound()
    {
        var result = await _sut.CheckoutAsync(new CheckoutCommand(1, 99), Key, clientIp: null);

        result.ErrorCode.Should().Be(ErrorCodes.ProductNotFound);
        _lock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_WithAnotherCustomersCard_ReturnsCardNotFound()
    {
        _cards.Setup(c => c.GetForCustomerAsync(7, 1, It.IsAny<CancellationToken>())).ReturnsAsync((CustomerCard?)null);

        var result = await _sut.CheckoutAsync(new CheckoutCommand(1, 1, CustomerCardId: 7), Key, clientIp: null);

        result.ErrorCode.Should().Be(ErrorCodes.CardNotFound);
        _gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_WithARepeatedKey_ReplaysWithoutCalling2C2P()
    {
        var request = new CheckoutCommand(1, 1);
        _transactions.Setup(t => t.GetByIdempotencyKeyAsync(Key, It.IsAny<CancellationToken>())).ReturnsAsync(Existing(request));

        var result = await _sut.CheckoutAsync(request, Key, clientIp: null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Replayed.Should().BeTrue();
        result.Value.InvoiceNo.Should().Be("INV1");
        _gateway.VerifyNoOtherCalls();
        _transactions.Verify(t => t.TryAddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutAsync_WithAKeyReusedForAnotherPurchase_ReturnsKeyReused()
    {
        _products.Setup(p => p.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Yoga);
        _transactions.Setup(t => t.GetByIdempotencyKeyAsync(Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Existing(new CheckoutCommand(1, 1)));

        var result = await _sut.CheckoutAsync(new CheckoutCommand(1, 2), Key, clientIp: null);

        result.ErrorCode.Should().Be(ErrorCodes.IdempotencyKeyReused);
        _gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_WhenTheLockStaysHeld_ReturnsInProgress()
    {
        var sut = new CheckoutServiceTests(lockWait: TimeSpan.Zero);
        sut._lock.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LockLease(LockStatus.Held));

        var result = await sut._sut.CheckoutAsync(new CheckoutCommand(1, 1), Key, clientIp: null);

        result.ErrorCode.Should().Be(ErrorCodes.CheckoutInProgress);
        sut._transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_WhenTheIndexBlocksASecondInsert_ReplaysTheWinner()
    {
        var request = new CheckoutCommand(1, 1);
        _invoices.Setup(i => i.NewInvoiceNo()).Returns("INV2");
        _transactions.SetupSequence(t => t.GetByIdempotencyKeyAsync(Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null)
            .ReturnsAsync(Existing(request));
        _transactions.Setup(t => t.TryAddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransactionInsertResult.DuplicateIdempotencyKey);

        var result = await _sut.CheckoutAsync(request, Key, clientIp: null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InvoiceNo.Should().Be("INV1", "the request that won the insert owns the purchase");
        _gateway.VerifyNoOtherCalls();
    }
}
