using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseInvoiceDetailTaxConfiguration
    : IEntityTypeConfiguration<PurchaseInvoiceDetailTax>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceDetailTax> builder)
    {
        builder.ToTable("purchase_invoice_detail_taxes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceDetailId)
            .HasColumnName("purchase_invoice_detail_id")
            .IsRequired();

        builder
            .Property(x => x.TaxCode)
            .HasColumnName("tax_code")
            .HasMaxLength(PurchaseInvoiceDetailTax.TaxCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxRateCode)
            .HasColumnName("tax_rate_code")
            .HasMaxLength(PurchaseInvoiceDetailTax.TaxRateCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxName)
            .HasColumnName("tax_name")
            .HasMaxLength(PurchaseInvoiceDetailTax.TaxNameMaxLen)
            .IsRequired();
        builder.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(10,4)");
        builder
            .Property(x => x.CalculationType)
            .HasColumnName("calculation_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.TaxableBase)
            .HasColumnName("taxable_base")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TaxAmount)
            .HasColumnName("tax_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<int>()
            .IsRequired();

        builder
            .HasIndex(x => x.PurchaseInvoiceDetailId)
            .HasDatabaseName("ix_purchase_invoice_detail_taxes_detail");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_purchase_invoice_detail_taxes_tenant");
    }
}
