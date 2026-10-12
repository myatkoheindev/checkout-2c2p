using Checkout.Application.Checkouts;
using Microsoft.Extensions.Time.Testing;

namespace Checkout.Application.Tests.Checkouts;

public sealed class InvoiceNumberGeneratorTests
{
    private readonly InvoiceNumberGenerator _generator =
        new(new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 23, 59, 0, TimeSpan.FromHours(8))));

    [Fact]
    public void Is_INV_plus_the_UTC_date_plus_ten_digits()
    {
        var invoiceNo = _generator.NewInvoiceNo();

        // 23:59 in Singapore is 15:59 UTC on the same day.
        invoiceNo.Should().MatchRegex("^INV20261009[0-9]{10}$");
        invoiceNo.Length.Should().BeLessThanOrEqualTo(50, "2C2P accepts at most 50 characters");
    }

    [Fact]
    public void Does_not_repeat_in_practice()
    {
        var invoices = Enumerable.Range(0, 1_000).Select(_ => _generator.NewInvoiceNo()).ToList();

        invoices.Should().OnlyHaveUniqueItems();
    }
}
