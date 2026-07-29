using ERP.Domain.Modules.Purchases.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseLinePvpAuditConfiguration
    : IEntityTypeConfiguration<PurchaseLinePvpAudit>
{
    public void Configure(EntityTypeBuilder<PurchaseLinePvpAudit> builder)
    {
        builder.ConfigureAuditBase("purchase_line_pvp_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
            .IsRequired();
        builder
            .Property(x => x.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder
            .Property(x => x.OldPvp)
            .HasColumnName("old_pvp")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.NewPvp)
            .HasColumnName("new_pvp")
            .HasColumnType("numeric(18,6)")
            .IsRequired();

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.EntityId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_purchase_line_pvp_audit_item_occurred_at");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.PurchaseInvoiceId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_purchase_line_pvp_audit_invoice_occurred_at");
    }
}
