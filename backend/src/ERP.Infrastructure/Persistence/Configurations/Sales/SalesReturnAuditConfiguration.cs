using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReturnAuditConfiguration : IEntityTypeConfiguration<SalesReturnAudit>
{
    public void Configure(EntityTypeBuilder<SalesReturnAudit> builder)
    {
        builder.ConfigureAuditBase("sales_return_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SalesInvoiceId).HasColumnName("sales_invoice_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder
            .Property(x => x.ReturnNumber)
            .HasColumnName("return_number")
            .HasMaxLength(SalesReturn.ReturnNumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.GrandTotal)
            .HasColumnName("grand_total")
            .HasColumnType("numeric(18,2)");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SalesInvoiceId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_sales_return_audit_sales_invoice_occurred_at");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CustomerId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_sales_return_audit_customer_occurred_at");
    }
}
