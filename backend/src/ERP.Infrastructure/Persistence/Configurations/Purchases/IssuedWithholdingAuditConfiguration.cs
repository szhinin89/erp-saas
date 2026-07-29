using ERP.Domain.Modules.Purchases.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class IssuedWithholdingAuditConfiguration
    : IEntityTypeConfiguration<IssuedWithholdingAudit>
{
    public void Configure(EntityTypeBuilder<IssuedWithholdingAudit> builder)
    {
        builder.ConfigureAuditBase("issued_withholding_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
            .IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.WithholdingNumber)
            .HasColumnName("withholding_number")
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.TotalRetained)
            .HasColumnName("total_retained")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.PurchaseInvoiceId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_issued_withholding_audit_invoice_occurred_at");
    }
}
