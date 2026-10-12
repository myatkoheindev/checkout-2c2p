using System.ComponentModel.DataAnnotations;

namespace Checkout.Application.Configuration;

/// <summary>The <c>Frontend</c> section of configuration: where the checkout UI is served from.</summary>
public sealed class FrontendOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Frontend";

    /// <summary>
    /// The frontend's origin as the browser sees it, such as <c>http://localhost:3000</c>. The CORS
    /// origin, and the base of the URL the payment provider sends the browser back to.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string Url { get; init; } = string.Empty;
}
