using System.Diagnostics;

namespace Checkout.Api.Extensions;

/// <summary>
/// Shapes every error the framework writes as RFC 7807 problem details, with the trace id so a
/// failure in the browser can be matched to a log line.
/// </summary>
internal static class ProblemDetailsCustomizer
{
    /// <summary>The extension member carrying the trace id.</summary>
    public const string TraceIdKey = "traceId";

    /// <summary>Applies the shared members to one problem details response.</summary>
    /// <param name="context">The problem being written.</param>
    public static void Customize(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions[TraceIdKey] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    }
}
