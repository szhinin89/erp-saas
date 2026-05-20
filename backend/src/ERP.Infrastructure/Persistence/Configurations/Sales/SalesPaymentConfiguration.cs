using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesPaymentConfiguration : IEntityTypeConfiguration<SalesPayment>
{
    public void Configure(EntityTypeBuilder<SalesPayment> builder)
    {
        builder.ToTable("sales_payment");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SalesDocumentId).HasColumnName("sales_document_id").IsRequired();
        builder.Property(e => e.PaymentMethod).HasColumnName("payment_method").HasMaxLength(5);
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 4);
        builder.Property(e => e.PaymentTerm).HasColumnName("payment_term");
        builder.Property(e => e.Bank).HasColumnName("bank").HasMaxLength(100);
        builder.Property(e => e.AccountNumber).HasColumnName("account_number").HasMaxLength(50);

        builder.HasIndex(e => e.SalesDocumentId).HasDatabaseName("ix_sales_payment_document");
    }
}
