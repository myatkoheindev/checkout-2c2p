using System.ComponentModel.DataAnnotations;

namespace Checkout.Infrastructure.External.TwoC2P;

/// <summary>The <c>TwoC2P</c> section of configuration.</summary>
/// <remarks>
/// Validated on first use rather than at startup, so the API (and its read endpoints) still run
/// before a secret key is configured. The sandbox merchant and key are on
/// https://developer.2c2p.com/docs/sandbox; Singapore is <c>JT01</c>.
/// </remarks>
public sealed class TwoC2POptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "TwoC2P";

    /// <summary>The 2C2P merchant id, e.g. <c>JT01</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string MerchantId { get; init; } = string.Empty;

    /// <summary>The merchant secret key that signs and verifies every JWT. Never commit it.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>The Payment API host, e.g. <c>https://sandbox-pgw.2c2p.com</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string BaseUrl { get; init; } = "https://sandbox-pgw.2c2p.com";

    /// <summary>The language for the hosted page and API messages.</summary>
    public string Locale { get; init; } = "en";

    /// <summary>How long one call to 2C2P may take.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
