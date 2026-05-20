using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchRetentionLineConfiguration : IEntityTypeConfiguration<PurchRetentionLine>
{
    public void Configure(EntityTypeBuilder<PurchRetentionLine> builder)
    {
        builder.ToTable("purch_retention_line");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(d => d.IssuedRetentionId).HasColumnName("issued_retention_id").IsRequired();
        builder.Property(d => d.TaxType).HasColumnName("tax_type").HasMaxLength(PurchRetentionLine.TaxTypeMaxLen).IsRequired();
        builder.Property(d => d.RetentionCode).HasColumnName("retention_code").HasMaxLength(PurchRetentionLine.RetentionCodeMaxLen).IsRequired();
        builder.Property(d => d.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.RetentionPct).HasColumnName("retention_pct").HasPrecision(9, 4).IsRequired();
        builder.Property(d => d.AmountRetained).HasColumnName("amount_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.RelatedInvoice).HasColumnName("related_invoice").HasMaxLength(PurchRetentionLine.RelatedInvoiceMaxLen);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(d => d.IssuedRetentionId).HasDatabaseName("ix_purch_retention_line_retention_id");
    }
}
