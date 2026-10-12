using System.Text;
using Checkout.Infrastructure.External.TwoC2P;

namespace Checkout.Infrastructure.Tests.External.TwoC2P;

public sealed class Hs256JwtTests
{
    private const string Secret = "unit-test-secret-not-a-real-merchant-key";

    [Fact]
    public void Verifies_a_token_signed_by_another_implementation()
    {
        // The jwt.io HS256 example, signed with "your-256-bit-secret": proves the encoding and the
        // key handling match a standard JWT library, as 2C2P's own samples use.
        const string token =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
            "eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ." +
            "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        Hs256Jwt.TryVerify(token, "your-256-bit-secret", out var payload).Should().BeTrue();
        payload.Should().Contain("\"name\":\"John Doe\"");
    }

    [Fact]
    public void Round_trips_a_payload()
    {
        var token = Hs256Jwt.Sign("""{"invoiceNo":"INV1","amount":35.00}""", Secret);

        Hs256Jwt.TryVerify(token, Secret, out var payload).Should().BeTrue();
        payload.Should().Be("""{"invoiceNo":"INV1","amount":35.00}""");
    }

    [Fact]
    public void Rejects_a_token_signed_with_another_key()
    {
        var token = Hs256Jwt.Sign("""{"respCode":"0000"}""", "attacker-key");

        Hs256Jwt.TryVerify(token, Secret, out _).Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_tampered_payload()
    {
        var parts = Hs256Jwt.Sign("""{"amount":35.00}""", Secret).Split('.');
        var forged = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"amount":0.01}""")).TrimEnd('=');

        Hs256Jwt.TryVerify($"{parts[0]}.{forged}.{parts[2]}", Secret, out _).Should().BeFalse();
    }

    [Fact]
    public void Rejects_the_none_algorithm()
    {
        // {"alg":"none","typ":"JWT"} . {"respCode":"0000"} . (no signature)
        const string token = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJyZXNwQ29kZSI6IjAwMDAifQ.";

        Hs256Jwt.TryVerify(token, Secret, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    [InlineData("!!!.###.$$$")]
    public void Rejects_malformed_input(string? token) =>
        Hs256Jwt.TryVerify(token, Secret, out _).Should().BeFalse();
}
