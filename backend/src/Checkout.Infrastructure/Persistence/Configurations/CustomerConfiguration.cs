using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Customer"/> to <c>customer</c>.</summary>
internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");
        builder.HasKey(c => c.Id);

        // Seeded with fixed ids, so ids are assigned by the seeder rather than AUTO_INCREMENT.
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(255).IsRequired();

        builder.HasIndex(c => c.Email).IsUnique();
    }
}
