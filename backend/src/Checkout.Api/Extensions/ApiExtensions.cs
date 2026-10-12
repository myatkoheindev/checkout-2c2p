using System.Text.Json;
using System.Text.Json.Serialization;
using Checkout.Api.Controllers;
using Checkout.Api.OpenApi;
using Checkout.Application.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Checkout.Api.Extensions;

/// <summary>Registers and wires the HTTP edge: controllers, JSON, problem details, CORS and OpenAPI.</summary>
public static class ApiExtensions
{
    /// <summary>The CORS policy that admits the checkout frontend.</summary>
    public const string FrontendCorsPolicy = "Frontend";

    /// <summary>Adds controllers and everything they rely on.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // camelCase is the default; enums go out as names ("OneTime", "Pending"), and numbers must be
        // JSON numbers (the web default also accepts "1", which muddies the schema). Controllers use
        // the MVC options; problem details and OpenAPI use the HTTP ones, so both get the same rules.
        services.AddControllers().AddJsonOptions(options => Configure(options.JsonSerializerOptions));
        services.ConfigureHttpJsonOptions(options => Configure(options.SerializerOptions));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<FrontendOptions>>((cors, frontend) =>
                cors.AddPolicy(FrontendCorsPolicy, policy => policy
                    .WithOrigins(frontend.Value.Url.TrimEnd('/'))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(CheckoutController.ReplayedHeader)));

        services.AddOpenApi(OpenApiConfiguration.Configure);

        return services;
    }

    /// <summary>Adds the request pipeline, outermost first, and maps the controllers.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors(FrontendCorsPolicy);

        // The document at /openapi/v1.json, browsable with Swagger UI at /swagger.
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Checkout API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Checkout API";
        });

        app.MapControllers();

        return app;
    }

    private static void Configure(JsonSerializerOptions json)
    {
        json.Converters.Add(new JsonStringEnumConverter());
        json.NumberHandling = JsonNumberHandling.Strict;
    }
}
