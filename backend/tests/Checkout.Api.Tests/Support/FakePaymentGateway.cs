using System.Collections.Concurrent;
using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Exceptions;
using Checkout.Infrastructure.External.TwoC2P;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Checkout.Api.Tests.Support;

/// <summary>
/// Payment-provider stand-in that records every call. <see cref="Delay"/> widens the race window,
/// so concurrent requests really do overlap while the first is "talking to the provider".
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    /// <summary>The secret key the test host is configured with; sign notifications with it.</summary>
    public const string SecretKey = "TEST-SECRET-KEY";

    /// <summary>Notifications are parsed by the real 2C2P code, so the tests cover its JWT and merchant checks.</summary>
    private static readonly TwoC2PGateway RealParser = new(
        new HttpClient(),
        Options.Create(new TwoC2POptions { MerchantId = "JT01", SecretKey = SecretKey }),
        NullLogger<TwoC2PGateway>.Instance);

    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(300);

    public ConcurrentQueue<PaymentSessionRequest> SessionRequests { get; } = new();

    public ConcurrentQueue<SavedCardChargeRequest> ChargeRequests { get; } = new();

    public ConcurrentQueue<string> StatusChecks { get; } = new();

    /// <summary>The session answer; defaults to success with a hosted-page URL (none for a saved card).</summary>
    public Func<PaymentSessionRequest, PaymentSession> OnCreateSession { get; init; } = request =>
        new(
            true,
            $"pt_{request.InvoiceNo}",
            request.SavedCardToken is null ? $"https://sandbox-pgw-ui.2c2p.com/payment/4.3/#/token/{request.InvoiceNo}" : null,
            "0000",
            "Success");

    /// <summary>The saved-card charge answer; defaults to submitted (confirm with a status check).</summary>
    public Func<SavedCardChargeRequest, SavedCardCharge> OnChargeSavedCard { get; init; } = _ =>
        new(ChargeOutcome.Submitted, null, "2000", "Transaction is completed, please do payment inquiry request for full payment information.");

    /// <summary>The status-check answer; defaults to a paid card.</summary>
    public Func<string, PaymentResult> OnStatusCheck { get; set; } = invoiceNo =>
        new(invoiceNo, PaymentStatus.Succeeded, 0m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "T123", null);

    /// <summary>Throws instead of answering, to simulate the provider being unreachable.</summary>
    public bool FailCreateSession { get; init; }

    public int SessionCalls => SessionRequests.Count;

    public async Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct = default)
    {
        SessionRequests.Enqueue(request);
        await Task.Delay(Delay, ct);

        return FailCreateSession
            ? throw new PaymentGatewayException("2C2P payment/4.3/paymenttoken timed out.")
            : OnCreateSession(request);
    }

    public async Task<SavedCardCharge> ChargeSavedCardAsync(SavedCardChargeRequest request, CancellationToken ct = default)
    {
        ChargeRequests.Enqueue(request);
        await Task.Delay(Delay, ct);
        return OnChargeSavedCard(request);
    }

    public Task<PaymentResult> GetPaymentStatusAsync(string invoiceNo, CancellationToken ct = default)
    {
        StatusChecks.Enqueue(invoiceNo);
        return Task.FromResult(OnStatusCheck(invoiceNo));
    }

    public PaymentResult? ParseNotification(string payload) => RealParser.ParseNotification(payload);
}
