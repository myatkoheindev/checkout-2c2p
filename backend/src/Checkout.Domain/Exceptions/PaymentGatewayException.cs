namespace Checkout.Domain.Exceptions;

/// <summary>
/// The payment provider could not be reached, timed out, or answered with something that is not
/// a valid response (including a bad signature). A declined payment is not an exception.
/// </summary>
public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException()
    {
    }

    public PaymentGatewayException(string message)
        : base(message)
    {
    }

    public PaymentGatewayException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
