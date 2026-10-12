using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CustomerCard"/> to <c>customer_card</c>.</summary>
internal sealed class CustomerCardConfiguration : IEntityTypeConfiguration<CustomerCard>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomerCard> builder)
    {
        builder.ToTable("customer_card");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerToken).HasMaxLength(100).IsRequired();
        builder.Property(c => c.MaskedCardNo).HasMaxLength(30).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnType("datetime");

        builder.HasOne(c => c.Customer)
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // A repeated notification carrying the same token must not save the card twice.
        builder.HasIndex(c => new { c.CustomerId, c.CustomerToken })
            .IsUnique()
            .HasDatabaseName("ux_customer_card_customer_token");
    }
}
