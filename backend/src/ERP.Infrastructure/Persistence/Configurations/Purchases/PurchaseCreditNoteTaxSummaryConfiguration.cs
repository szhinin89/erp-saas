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
        builder.ToTable("purchase_credit_note_tax_summaries");

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
            .Property(x => x.TaxableBase)
            .HasColumnName("taxable_base")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // ── Computed properties (NOT persisted) ─────────────────────────────
        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2 — corrección post-revisión):
        // VatCode/.../IrbpnrAmount son legacy compatibility mirror derivado de Taxes — nunca
        // columnas propias. Ver comentario de la entidad.
        builder.Ignore(x => x.VatCode);
        builder.Ignore(x => x.VatRate);
        builder.Ignore(x => x.VatName);
        builder.Ignore(x => x.VatAmount);
        builder.Ignore(x => x.IceCode);
        builder.Ignore(x => x.IceRate);
        builder.Ignore(x => x.IceName);
        builder.Ignore(x => x.IceAmount);
        builder.Ignore(x => x.IrbpnrCode);
        builder.Ignore(x => x.IrbpnrRate);
        builder.Ignore(x => x.IrbpnrName);
        builder.Ignore(x => x.IrbpnrAmount);

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

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-2)
        builder
            .HasMany(x => x.Taxes)
            .WithOne()
            .HasForeignKey(x => x.PurchaseCreditNoteTaxSummaryId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──────────────────────────────────────────────────────
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BranchId,
            })
            .HasDatabaseName("ix_purchase_credit_note_tax_summaries_tenant_company_branch");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseCreditNoteId })
            .HasDatabaseName("ix_purchase_credit_note_tax_summaries_tenant_credit_note");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseInvoiceId })
            .HasDatabaseName("ix_purchase_credit_note_tax_summaries_tenant_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.SourcePurchaseInvoiceTaxSummaryId })
            .HasDatabaseName("ix_purchase_credit_note_tax_summaries_tenant_source_summary");
    }
}
