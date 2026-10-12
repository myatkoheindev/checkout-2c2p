using System.ComponentModel.DataAnnotations;

namespace Checkout.Application.Configuration;

/// <summary>The <c>Application</c> section of configuration: facts about this deployment.</summary>
public sealed class ApplicationOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Application";

    /// <summary>The service's display name, reported by the health endpoint.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Name { get; init; } = string.Empty;
}
