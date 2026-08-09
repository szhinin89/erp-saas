using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>FLOW-READY-02C-R1.2 — mapeo EF de <see cref="PurchaseCreditNoteTaxSummary"/>.</summary>
public sealed class PurchaseCreditNoteTaxSummaryConfiguration
    : IEntityTypeConfiguration<PurchaseCreditNoteTaxSummary>
{
    public void Configure(EntityTypeBuilder<PurchaseCreditNoteTaxSummary> builder)
    {
        builder.ToTable("purchase_cn_tax_summaries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder
            .Property(x => x.PurchaseCreditNoteId)
            .HasColumnName("purchase_credit_note_id")
            .IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
            .IsRequired();
        builder
            .Property(x => x.SourcePurchaseInvoiceTaxSummaryId)
            .HasColumnName("source_purchase_invoice_tax_summary_id")
            .IsRequired();

        builder
            .Property(x => x.VatCode)
            .HasColumnName("vat_code")
            .HasMaxLength(PurchaseInvoiceTaxSummary.VatCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.VatRate)
            .HasColumnName("vat_rate")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.VatName)
            .HasColumnName("vat_name")
            .HasMaxLength(PurchaseInvoiceTaxSummary.VatNameMaxLen);

        builder
            .Property(x => x.IceCode)
            .HasColumnName("ice_code")
            .HasMaxLength(PurchaseInvoiceTaxSummary.IceCodeMaxLen);
        builder
            .Property(x => x.IceRate)
            .HasColumnName("ice_rate")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.IceName)
            .HasColumnName("ice_name")
            .HasMaxLength(PurchaseInvoiceTaxSummary.IceNameMaxLen);

        builder
            .Property(x => x.TaxableBase)
            .HasColumnName("taxable_base")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.IceAmount)
            .HasColumnName("ice_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.VatAmount)
            .HasColumnName("vat_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // ── Relationships ────────────────────────────────────────────────
        // La relación con PurchaseCreditNote.TaxSummaries se configura desde
        // PurchaseCreditNoteConfiguration (mismo patrón que Lines — HasMany del lado del padre).
        builder
            .HasOne<PurchaseInvoice>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Referencia (no copia) al resumen fiscal de compra de origen — nunca se elimina en cascada.
        builder
            .HasOne<PurchaseInvoiceTaxSummary>()
            .WithMany()
            .HasForeignKey(x => x.SourcePurchaseInvoiceTaxSummaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BranchId,
            })
            .HasDatabaseName("ix_purchase_cn_tax_summaries_tenant_company_branch");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseCreditNoteId })
            .HasDatabaseName("ix_purchase_cn_tax_summaries_tenant_credit_note");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseInvoiceId })
            .HasDatabaseName("ix_purchase_cn_tax_summaries_tenant_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.SourcePurchaseInvoiceTaxSummaryId })
            .HasDatabaseName("ix_purchase_cn_tax_summaries_tenant_source_summary");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.PurchaseCreditNoteId,
                x.VatCode,
                x.IceCode,
            })
            .HasDatabaseName("ix_purchase_cn_tax_summaries_tenant_credit_note_vat_ice");
    }
}
