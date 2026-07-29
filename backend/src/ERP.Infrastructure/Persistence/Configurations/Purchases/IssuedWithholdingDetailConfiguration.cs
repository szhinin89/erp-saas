using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class IssuedWithholdingDetailConfiguration
    : IEntityTypeConfiguration<IssuedWithholdingDetail>
{
    public void Configure(EntityTypeBuilder<IssuedWithholdingDetail> builder)
    {
        builder.ToTable("issued_withholding_details");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.WithholdingId).HasColumnName("withholding_id").IsRequired();
        builder
            .Property(x => x.TaxType)
            .HasColumnName("tax_type")
            .HasMaxLength(IssuedWithholdingDetail.TaxTypeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.RetentionCode)
            .HasColumnName("retention_code")
            .HasMaxLength(IssuedWithholdingDetail.RetentionCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.RetentionCodeDescription)
            .HasColumnName("retention_code_description")
            .HasMaxLength(IssuedWithholdingDetail.CodeDescMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxableBase)
            .HasColumnName("taxable_base")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.RetentionPct)
            .HasColumnName("retention_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.AmountRetained)
            .HasColumnName("amount_retained")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.HasIndex(x => x.WithholdingId).HasDatabaseName("ix_issued_wh_details_withholding");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_issued_wh_details_tenant");
    }
}
