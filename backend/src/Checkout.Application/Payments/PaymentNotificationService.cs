using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Common;

namespace Checkout.Application.Payments;

/// <summary>Default <see cref="IPaymentNotificationService"/>.</summary>
public sealed class PaymentNotificationService(IPaymentGateway gateway, IPaymentConfirmationService confirmation) : IPaymentNotificationService
{
    /// <inheritdoc />
    public async Task<Result<ConfirmationOutcome>> HandleAsync(string? payload, CancellationToken ct = default)
    {
        var result = string.IsNullOrWhiteSpace(payload) ? null : gateway.ParseNotification(payload);
        if (result is null)
        {
            return Result<ConfirmationOutcome>.Failure("The notification could not be verified.", ErrorCodes.InvalidSignature);
        }

        return Result<ConfirmationOutcome>.Success(await confirmation.ApplyAsync(result, ct).ConfigureAwait(false));
    }
}
