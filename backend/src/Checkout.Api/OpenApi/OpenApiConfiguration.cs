using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Checkout.Api.OpenApi;

/// <summary>Configures the OpenAPI document generated from the controllers.</summary>
internal static class OpenApiConfiguration
{
    /// <summary>
    /// Suffixes stripped from schema names, so Swagger shows the resource: <c>CustomerResponse</c> is
    /// published as <c>Customer</c>.
    /// </summary>
    private static readonly string[] SchemaSuffixes = ["Response", "Dto"];

    /// <summary>The header every checkout retry must repeat.</summary>
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Sets the document info, schema names, the Idempotency-Key header and the examples.</summary>
    /// <param name="options">The OpenAPI options.</param>
    public static void Configure(OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CreateSchemaReferenceId = type =>
        {
            var id = OpenApiOptions.CreateDefaultSchemaReferenceId(type);
            var suffix = id is null ? null : SchemaSuffixes.FirstOrDefault(sfx => id.Length > sfx.Length && id.EndsWith(sfx, StringComparison.Ordinal));
            return suffix is null ? id : id![..^suffix.Length];
        };

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Checkout API",
                Version = "1.0.0",
                Description = "Checkout with 2C2P (sandbox, Payment API v4.3) in SGD. Errors are RFC 7807 problem details.",
            };

            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            DescribeIdempotencyKey(operation);

            // MVC's JSON input also lists "application/*+json"; the API documents plain JSON only.
            operation.RequestBody?.Content?.Remove("application/*+json");

            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var examples = metadata.OfType<ResponseExampleAttribute>()
                .Select(e => (StatusCode: (int?)e.StatusCode, e.ContentType, e.Example))
                .Concat(metadata.OfType<RequestExampleAttribute>().Select(e => (StatusCode: (int?)null, ContentType: "application/json", e.Example)))
                .ToList();

            if (examples.Count == 0)
            {
                return Task.CompletedTask;
            }

            // Examples are serialized exactly as responses are: camelCase, enums as names.
            var json = context.ApplicationServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

            foreach (var (statusCode, contentType, name) in examples)
            {
                var value = typeof(ApiExamples).GetProperty(name)?.GetValue(null)
                    ?? throw new InvalidOperationException($"ApiExamples has no example named {name}.");
                var node = JsonSerializer.SerializeToNode(value, value.GetType(), json);
                var content = statusCode is { } status ? ResponseContent(operation, status) : operation.RequestBody?.Content;

                if (content is null)
                {
                    continue;
                }

                if (!content.TryGetValue(contentType, out var mediaType))
                {
                    mediaType = new OpenApiMediaType();
                    content[contentType] = mediaType;
                }

                mediaType.Example = node;
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>Marks the <c>Idempotency-Key</c> header required and explains it, wherever an action takes it.</summary>
    private static void DescribeIdempotencyKey(OpenApiOperation operation)
    {
        if (operation.Parameters?.FirstOrDefault(p => p.Name == IdempotencyKeyHeader) is not OpenApiParameter header)
        {
            return;
        }

        header.Required = true;
        header.Description =
            "A UUID identifying this purchase. Generate it when the checkout page loads and " +
            "send the same value on every retry.";
        header.Example = System.Text.Json.Nodes.JsonValue.Create("3f2c9a1e-6b7d-4c58-9e0a-2d4b8f1c7e63");
    }

    private static IDictionary<string, OpenApiMediaType>? ResponseContent(OpenApiOperation operation, int status)
    {
        var key = status.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return operation.Responses is not null && operation.Responses.TryGetValue(key, out var response)
            ? response.Content
            : null;
    }
}
