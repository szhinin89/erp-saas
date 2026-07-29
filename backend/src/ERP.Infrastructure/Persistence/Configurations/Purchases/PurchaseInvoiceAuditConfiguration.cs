using ERP.Domain.Modules.Purchases.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseInvoiceAuditConfiguration
    : IEntityTypeConfiguration<PurchaseInvoiceAudit>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceAudit> builder)
    {
        builder.ConfigureAuditBase("purchase_invoice_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(30)
            .IsRequired();
        builder
            .Property(x => x.GrandTotal)
            .HasColumnName("grand_total")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SupplierId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_purchase_invoice_audit_supplier_occurred_at");
    }
}
