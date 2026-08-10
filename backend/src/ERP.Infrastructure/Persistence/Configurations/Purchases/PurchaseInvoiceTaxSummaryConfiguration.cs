using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>FLOW-READY-02D.1 — mapeo EF de <see cref="PurchaseInvoiceTaxSummary"/>.</summary>
public sealed class PurchaseInvoiceTaxSummaryConfiguration
    : IEntityTypeConfiguration<PurchaseInvoiceTaxSummary>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceTaxSummary> builder)
    {
        builder.ToTable("purchase_invoice_tax_summaries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
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

        // FLOW-READY-02F.1 — dimensión IRBPNR, aditiva a la clave de agrupación Vat/Ice existente.
        builder
            .Property(x => x.IrbpnrCode)
            .HasColumnName("irbpnr_code")
            .HasMaxLength(PurchaseInvoiceDetailTax.TaxRateCodeMaxLen);
        builder
            .Property(x => x.IrbpnrRate)
            .HasColumnName("irbpnr_rate")
            .HasColumnType("numeric(10,4)")
            .HasDefaultValue(0m)
            .IsRequired();
        builder
            .Property(x => x.IrbpnrName)
            .HasColumnName("irbpnr_name")
            .HasMaxLength(PurchaseInvoiceDetailTax.TaxNameMaxLen);
        builder
            .Property(x => x.IrbpnrAmount)
            .HasColumnName("irbpnr_amount")
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

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
        // La relación con PurchaseInvoice.TaxSummaries se configura desde PurchaseInvoiceConfiguration
        // (mismo patrón que Lines/PaymentSchedules — HasMany del lado del agregado padre).
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
            .HasDatabaseName("ix_purchase_invoice_tax_summaries_tenant_company_branch");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseInvoiceId })
            .HasDatabaseName("ix_purchase_invoice_tax_summaries_tenant_invoice");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.PurchaseInvoiceId,
                x.VatCode,
                x.IceCode,
            })
            .HasDatabaseName("ix_purchase_invoice_tax_summaries_tenant_invoice_vat_ice");
    }
}
