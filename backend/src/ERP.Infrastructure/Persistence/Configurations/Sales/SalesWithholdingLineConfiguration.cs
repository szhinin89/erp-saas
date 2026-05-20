using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesWithholdingLineConfiguration : IEntityTypeConfiguration<SalesWithholdingLine>
{
    public void Configure(EntityTypeBuilder<SalesWithholdingLine> builder)
    {
        builder.ToTable("sales_withholding_line");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.SalesWithholdingId).HasColumnName("sales_withholding_id").IsRequired();
        builder.Property(e => e.TaxType).HasColumnName("tax_type").HasMaxLength(SalesWithholdingLine.TaxTypeMaxLen).IsRequired();
        builder.Property(e => e.RetentionCode).HasColumnName("retention_code").HasMaxLength(SalesWithholdingLine.RetentionCodeMaxLen).IsRequired();
        builder.Property(e => e.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.RetentionPct).HasColumnName("retention_pct").HasPrecision(9, 4).IsRequired();
        builder.Property(e => e.AmountRetained).HasColumnName("amount_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.UpdatedBy);
        builder.Ignore(e => e.CreatedBy);
    }
}
