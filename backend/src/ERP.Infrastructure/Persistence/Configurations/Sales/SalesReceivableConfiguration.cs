using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReceivableConfiguration : IEntityTypeConfiguration<SalesReceivable>
{
    public void Configure(EntityTypeBuilder<SalesReceivable> builder)
    {
        builder.ToTable("sales_receivables");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();

        builder
            .Property(x => x.OriginalAmount)
            .HasColumnName("original_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(SalesReceivable.StatusMaxLen)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(x => x.BalanceDue);

        builder
            .HasMany(x => x.Installments)
            .WithOne()
            .HasForeignKey(x => x.ReceivableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SalesInvoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => x.InvoiceId)
            .IsUnique()
            .HasDatabaseName("uq_sales_receivables_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_sales_receivables_tenant_company");

        builder
            .HasIndex(x => new { x.TenantId, x.CustomerId })
            .HasDatabaseName("ix_sales_receivables_tenant_customer");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_sales_receivables_tenant_status");
    }
}
