using Checkout.Application.Checkouts;
using Checkout.Application.Checkouts.Validators;
using Checkout.Application.Configuration;
using Checkout.Application.Customers;
using Checkout.Application.Health;
using Checkout.Application.Payments;
using Checkout.Application.Products;
using Checkout.Application.Transactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Checkout.Application;

/// <summary>Registers the Application layer: options, validators and use-case services.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the use cases: options (validated at startup), validators, and services. Services are
    /// scoped to the request; stateless helpers are singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ApplicationOptions>()
            .BindConfiguration(ApplicationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<FrontendOptions>()
            .BindConfiguration(FrontendOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<CheckoutOptions>()
            .BindConfiguration(CheckoutOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddValidatorsFromAssemblyContaining<CheckoutCommandValidator>();

        // Singleton: stateless and thread-safe.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInvoiceNumberGenerator, InvoiceNumberGenerator>();

        // Scoped: one instance per HTTP request, sharing the request's DbContext.
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IPaymentConfirmationService, PaymentConfirmationService>();
        services.AddScoped<IPaymentNotificationService, PaymentNotificationService>();
        services.AddScoped<ITransactionService, TransactionService>();

        return services;
    }
}
