using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Product"/> to <c>product</c>.</summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Price).HasColumnType("decimal(12,2)");
        builder.Property(p => p.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(p => p.PaymentType)
            .HasConversion<string>()
            .HasColumnType(MySqlEnum.Of<PaymentType>());
    }
}
