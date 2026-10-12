namespace Checkout.Domain.Entities;

/// <summary>A person who can be charged. Seeded; there is no customer management.</summary>
public sealed class Customer
{
    /// <summary>For EF Core.</summary>
    private Customer()
    {
    }

    /// <summary>Creates a customer.</summary>
    /// <param name="id">The fixed seed id.</param>
    /// <param name="firstName">Given name.</param>
    /// <param name="lastName">Family name.</param>
    /// <param name="email">Contact email.</param>
    public Customer(int id, string firstName, string lastName, string email)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public int Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;
}
