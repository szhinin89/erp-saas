using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesInvoiceDetailTaxConfiguration : IEntityTypeConfiguration<SalesInvoiceDetailTax>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceDetailTax> builder)
    {
        builder.ToTable("sales_invoice_detail_taxes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.SalesInvoiceDetailId)
            .HasColumnName("sales_invoice_detail_id")
            .IsRequired();

        builder
            .Property(x => x.TaxCode)
            .HasColumnName("tax_code")
            .HasMaxLength(SalesInvoiceDetailTax.TaxCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxRateCode)
            .HasColumnName("tax_rate_code")
            .HasMaxLength(SalesInvoiceDetailTax.TaxRateCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxName)
            .HasColumnName("tax_name")
            .HasMaxLength(SalesInvoiceDetailTax.TaxNameMaxLen)
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
            .HasIndex(x => x.SalesInvoiceDetailId)
            .HasDatabaseName("ix_sales_invoice_detail_taxes_detail");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_sales_invoice_detail_taxes_tenant");
    }
}
