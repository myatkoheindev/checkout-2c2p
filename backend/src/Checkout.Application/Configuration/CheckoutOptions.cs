using System.ComponentModel.DataAnnotations;

namespace Checkout.Application.Configuration;

/// <summary>The <c>Checkout</c> section of configuration.</summary>
public sealed class CheckoutOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Checkout";

    /// <summary>
    /// This API's public URL, which the payment provider must be able to reach for its notification —
    /// an ngrok or Cloudflare Tunnel URL when running locally.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string PublicApiUrl { get; init; } = string.Empty;

    /// <summary>
    /// How long a checkout holds its idempotency lock. Longer than the slowest checkout (three
    /// provider calls for a saved card); if it still expires, the MySQL unique index takes over.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan LockTtl { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// How long a request with the same key waits for the first to finish before answering 409.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The shortest gap between two Payment Inquiries for one invoice.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan InquiryThrottle { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How often a waiting request retries the lock.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:00:05")]
    public TimeSpan LockPollInterval { get; init; } = TimeSpan.FromMilliseconds(100);
}
