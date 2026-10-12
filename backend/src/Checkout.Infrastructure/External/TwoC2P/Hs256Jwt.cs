using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Checkout.Infrastructure.External.TwoC2P;

/// <summary>
/// The JWT 2C2P uses: HS256, signed with the merchant secret key taken as UTF-8 text.
/// See https://developer.2c2p.com/docs/json-web-tokens-jwt.
/// </summary>
/// <remarks>
/// Deliberately minimal — one algorithm, no claims processing — so verification cannot be
/// talked into another algorithm (such as <c>none</c>) by the token's own header.
/// </remarks>
internal static class Hs256Jwt
{
    private static readonly byte[] EncodedHeader =
        Encoding.UTF8.GetBytes(Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}"""u8));

    /// <summary>Signs a JSON payload.</summary>
    /// <param name="payloadJson">The payload, already serialized.</param>
    /// <param name="secretKey">The merchant secret key.</param>
    /// <returns>The compact JWT <c>header.payload.signature</c>.</returns>
    public static string Sign(string payloadJson, string secretKey)
    {
        var signingInput = $"{Encoding.UTF8.GetString(EncodedHeader)}.{Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson))}";
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(signingInput));

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    /// <summary>Verifies a token's signature and returns its payload.</summary>
    /// <param name="token">The compact JWT.</param>
    /// <param name="secretKey">The merchant secret key.</param>
    /// <param name="payloadJson">The decoded payload when valid.</param>
    /// <returns><c>true</c> only when the token is HS256 and the signature matches.</returns>
    public static bool TryVerify(string? token, string secretKey, out string payloadJson)
    {
        payloadJson = string.Empty;

        var parts = token?.Split('.');
        if (parts is not { Length: 3 } || parts.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        try
        {
            using (var header = JsonDocument.Parse(Base64UrlDecode(parts[0])))
            {
                if (!header.RootElement.TryGetProperty("alg", out var alg) || alg.GetString() != "HS256")
                {
                    return false;
                }
            }

            var expected = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secretKey),
                Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));

            if (!CryptographicOperations.FixedTimeEquals(expected, Base64UrlDecode(parts[2])))
            {
                return false;
            }

            payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');

        return Convert.FromBase64String(base64);
    }
}
