using Checkout.Application.Checkouts.DTOs;
using FluentValidation;

namespace Checkout.Application.Checkouts.Validators;

/// <summary>Shape checks for a checkout body. Whether the ids exist is the service's job.</summary>
public sealed class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(r => r.CustomerId).GreaterThan(0);
        RuleFor(r => r.ProductId).GreaterThan(0);
        RuleFor(r => r.CustomerCardId).GreaterThan(0).When(r => r.CustomerCardId is not null);
    }
}
