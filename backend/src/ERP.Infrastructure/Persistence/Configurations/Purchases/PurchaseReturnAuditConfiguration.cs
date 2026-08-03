using ERP.Domain.Modules.Purchases.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>Mapeo EF de <see cref="PurchaseReturnAudit"/> (ADR-022, Entity Audit) — diseño P0-02 §20.1.</summary>
public sealed class PurchaseReturnAuditConfiguration : IEntityTypeConfiguration<PurchaseReturnAudit>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnAudit> builder)
    {
        builder.ConfigureAuditBase("purchase_return_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.PurchaseInvoiceId).HasColumnName("purchase_invoice_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id");
        builder
            .Property(x => x.ReturnNumber)
            .HasColumnName("return_number")
            .HasMaxLength(PurchaseReturn.ReturnNumberMaxLen);
        builder
            .Property(x => x.GrandTotal)
            .HasColumnName("grand_total")
            .HasColumnType("numeric(18,2)");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.PurchaseInvoiceId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_purchase_return_audit_purchase_invoice_occurred_at");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SupplierId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_purchase_return_audit_supplier_occurred_at");
    }
}
