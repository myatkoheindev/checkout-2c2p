namespace Checkout.Api.OpenApi;

/// <summary>
/// Shows an example response in Swagger. <paramref name="example"/> names a static property of
/// <see cref="ApiExamples"/>; attributes can only hold constants, so the example object lives there.
/// </summary>
/// <param name="statusCode">The response the example belongs to.</param>
/// <param name="example">The <see cref="ApiExamples"/> property, e.g. <c>nameof(ApiExamples.Customers)</c>.</param>
/// <param name="contentType">The media type of that response.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class ResponseExampleAttribute(int statusCode, string example, string contentType = "application/json") : Attribute
{
    public int StatusCode { get; } = statusCode;

    public string Example { get; } = example;

    public string ContentType { get; } = contentType;
}

/// <summary>Shows an example request body in Swagger; see <see cref="ResponseExampleAttribute"/>.</summary>
/// <param name="example">The <see cref="ApiExamples"/> property.</param>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequestExampleAttribute(string example) : Attribute
{
    public string Example { get; } = example;
}
