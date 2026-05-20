using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesRetentionLineConfiguration : IEntityTypeConfiguration<SalesRetentionLine>
{
    public void Configure(EntityTypeBuilder<SalesRetentionLine> builder)
    {
        builder.ToTable("sales_retention_line");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.SalesRetentionId).HasColumnName("sales_retention_id").IsRequired();
        builder.Property(e => e.TaxType).HasColumnName("tax_type").HasMaxLength(SalesRetentionLine.TaxTypeMaxLen).IsRequired();
        builder.Property(e => e.RetentionCode).HasColumnName("retention_code").HasMaxLength(SalesRetentionLine.RetentionCodeMaxLen).IsRequired();
        builder.Property(e => e.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.RetentionPct).HasColumnName("retention_pct").HasPrecision(9, 4).IsRequired();
        builder.Property(e => e.AmountRetained).HasColumnName("amount_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.SalesRetentionId).HasDatabaseName("ix_sales_retention_line_retention_id");
    }
}
