using Checkout.Application.Customers.DTOs;

namespace Checkout.Api.Contracts.Responses;

/// <summary>A customer as the checkout page lists it.</summary>
/// <param name="Id">The customer id.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">Contact email.</param>
public sealed record CustomerResponse(int Id, string FirstName, string LastName, string Email)
{
    /// <summary>The name shown in the payment summary.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Maps the use-case result.</summary>
    public static CustomerResponse From(CustomerDto customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new(customer.Id, customer.FirstName, customer.LastName, customer.Email);
    }
}
