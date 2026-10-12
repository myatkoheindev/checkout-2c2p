using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.OpenApi;

/// <summary>Problem details examples shared by several endpoints.</summary>
internal static class ProblemExamples
{
    /// <summary>The media type of every error response.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>A 404 for an id that does not exist.</summary>
    /// <param name="detail">What was not found.</param>
    /// <param name="instance">The request path.</param>
    /// <returns>The example.</returns>
    public static ProblemDetails NotFound(string detail, string instance) => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
        Instance = instance,
        Extensions = { ["traceId"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" },
    };
}
