using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

/// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-3).</summary>
public sealed class SalesReturnDetailTaxConfiguration : IEntityTypeConfiguration<SalesReturnDetailTax>
{
    public void Configure(EntityTypeBuilder<SalesReturnDetailTax> builder)
    {
        builder.ToTable("sales_return_detail_taxes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.SalesReturnDetailId)
            .HasColumnName("sales_return_detail_id")
            .IsRequired();

        builder
            .Property(x => x.TaxCode)
            .HasColumnName("tax_code")
            .HasMaxLength(SalesReturnDetailTax.TaxCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxRateCode)
            .HasColumnName("tax_rate_code")
            .HasMaxLength(SalesReturnDetailTax.TaxRateCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxName)
            .HasColumnName("tax_name")
            .HasMaxLength(SalesReturnDetailTax.TaxNameMaxLen)
            .IsRequired();
        builder.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(10,4)");
        builder
            .Property(x => x.CalculationType)
            .HasColumnName("calculation_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.TaxAmount)
            .HasColumnName("tax_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .HasIndex(x => x.SalesReturnDetailId)
            .HasDatabaseName("ix_sales_return_detail_taxes_detail");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_sales_return_detail_taxes_tenant");
    }
}
