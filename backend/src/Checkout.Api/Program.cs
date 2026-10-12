using Checkout.Api.Extensions;
using Checkout.Application;
using Checkout.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: each layer registers its own services.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

// Migrations and seed data are applied on startup, so `docker compose up` needs no extra step.
// Turn off with Database:MigrateOnStartup=false where migrations are run separately.
if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseApi();

await app.RunAsync();

/// <summary>The entry point, made visible to <c>WebApplicationFactory</c> in the tests.</summary>
public partial class Program;
