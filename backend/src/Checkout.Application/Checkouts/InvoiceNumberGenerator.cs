using System.Globalization;
using System.Security.Cryptography;

namespace Checkout.Application.Checkouts;

/// <summary>
/// <c>INV</c> + the UTC date + 10 random digits: 21 alphanumeric characters, inside 2C2P's 50,
/// readable by support, and not guessable from a neighbouring invoice.
/// </summary>
public sealed class InvoiceNumberGenerator(TimeProvider timeProvider) : IInvoiceNumberGenerator
{
    /// <summary>The prefix every invoice number starts with.</summary>
    public const string Prefix = "INV";

    /// <inheritdoc />
    public string NewInvoiceNo()
    {
        var date = timeProvider.GetUtcNow().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        // Two 5-digit halves: 10 random digits without overflowing Int32.
        var high = RandomNumberGenerator.GetInt32(0, 100_000);
        var low = RandomNumberGenerator.GetInt32(0, 100_000);

        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}{date}{high:D5}{low:D5}");
    }
}
