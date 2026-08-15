using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseReceptionLineTaxConfiguration
    : IEntityTypeConfiguration<PurchaseReceptionLineTax>
{
    public void Configure(EntityTypeBuilder<PurchaseReceptionLineTax> builder)
    {
        builder.ToTable("purchase_reception_line_taxes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.PurchaseReceptionLineId)
            .HasColumnName("purchase_reception_line_id")
            .IsRequired();

        builder
            .Property(x => x.TaxCode)
            .HasColumnName("tax_code")
            .HasMaxLength(PurchaseReceptionLineTax.TaxCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxRateCode)
            .HasColumnName("tax_rate_code")
            .HasMaxLength(PurchaseReceptionLineTax.TaxRateCodeMaxLen)
            .IsRequired();
        builder.Property(x => x.Tarifa).HasColumnName("rate").HasColumnType("numeric(10,4)").IsRequired();
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
            .HasIndex(x => x.PurchaseReceptionLineId)
            .HasDatabaseName("ix_purchase_reception_line_taxes_line");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_purchase_reception_line_taxes_tenant");
    }
}
