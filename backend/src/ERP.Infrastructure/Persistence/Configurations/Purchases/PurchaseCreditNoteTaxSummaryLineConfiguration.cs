using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2 — corrección post-revisión).</summary>
public sealed class PurchaseCreditNoteTaxSummaryLineConfiguration
    : IEntityTypeConfiguration<PurchaseCreditNoteTaxSummaryLine>
{
    public void Configure(EntityTypeBuilder<PurchaseCreditNoteTaxSummaryLine> builder)
    {
        builder.ToTable("purchase_credit_note_tax_summary_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.PurchaseCreditNoteTaxSummaryId)
            .HasColumnName("purchase_credit_note_tax_summary_id")
            .IsRequired();

        builder
            .Property(x => x.TaxCode)
            .HasColumnName("tax_code")
            .HasMaxLength(PurchaseCreditNoteTaxSummaryLine.TaxCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxRateCode)
            .HasColumnName("tax_rate_code")
            .HasMaxLength(PurchaseCreditNoteTaxSummaryLine.TaxRateCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TaxName)
            .HasColumnName("tax_name")
            .HasMaxLength(PurchaseCreditNoteTaxSummaryLine.TaxNameMaxLen)
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
            .HasIndex(x => x.PurchaseCreditNoteTaxSummaryId)
            .HasDatabaseName("ix_purchase_credit_note_tax_summary_lines_summary");
        builder
            .HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_purchase_credit_note_tax_summary_lines_tenant");
    }
}
