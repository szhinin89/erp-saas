using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseWithholdingLineConfiguration : IEntityTypeConfiguration<PurchaseWithholdingLine>
{
    public void Configure(EntityTypeBuilder<PurchaseWithholdingLine> builder)
    {
        builder.ToTable("purchase_withholding_line");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.PurchaseWithholdingId).HasColumnName("purchase_withholding_id").IsRequired();
        builder.Property(e => e.TaxType).HasColumnName("tax_type").HasMaxLength(PurchaseWithholdingLine.TaxTypeMaxLen).IsRequired();
        builder.Property(e => e.RetentionCode).HasColumnName("retention_code").HasMaxLength(PurchaseWithholdingLine.RetentionCodeMaxLen).IsRequired();
        builder.Property(e => e.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.RetentionPct).HasColumnName("retention_pct").HasPrecision(9, 4).IsRequired();
        builder.Property(e => e.AmountRetained).HasColumnName("amount_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.UpdatedBy);
        builder.Ignore(e => e.CreatedBy);
    }
}
