using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PaymentTransaction"/> to <c>transaction</c>.</summary>
internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("transaction");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.InvoiceNo).HasMaxLength(50).IsRequired();
        builder.Property(t => t.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(t => t.RequestHash).HasColumnType("char(64)").IsRequired();
        builder.Property(t => t.Flow)
            .HasConversion<string>()
            .HasColumnType(MySqlEnum.Of<PaymentFlow>());
        builder.Property(t => t.Amount).HasColumnType("decimal(12,2)");
        builder.Property(t => t.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasColumnType(MySqlEnum.Of<TransactionStatus>());
        builder.Property(t => t.PaymentToken).HasMaxLength(255);
        builder.Property(t => t.RedirectUrl).HasMaxLength(2048);
        builder.Property(t => t.PaymentChannel).HasMaxLength(20);
        builder.Property(t => t.MaskedCardNo).HasMaxLength(30);
        builder.Property(t => t.ProviderRef).HasMaxLength(50);
        builder.Property(t => t.RespCode).HasMaxLength(10);
        builder.Property(t => t.RespDesc).HasMaxLength(100);
        builder.Property(t => t.CreatedAt).HasColumnType("datetime");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime");
        builder.Property(t => t.PaidAt).HasColumnType("datetime");

        builder.HasOne(t => t.Customer).WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CustomerCard).WithMany().HasForeignKey(t => t.CustomerCardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.InvoiceNo).IsUnique().HasDatabaseName("ux_transaction_invoice_no");

        // The last line of defence against a double charge: if the Redis lock expires or Redis
        // is down, a second insert for the same key fails here.
        builder.HasIndex(t => t.IdempotencyKey).IsUnique().HasDatabaseName("ux_transaction_idempotency_key");
    }
}
