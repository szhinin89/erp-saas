using ERP.Domain.Modules.Commercial.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Commercial;

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("sales_order");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PublicId).HasColumnName("public_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.OrderNumber).HasColumnName("order_number").HasMaxLength(SalesOrder.NumberMaxLen).IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.RequiredDate).HasColumnName("required_date");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(SalesOrder.StatusMaxLen).IsRequired();
        builder.Property(e => e.CurrencyCode).HasColumnName("currency_code").HasMaxLength(SalesOrder.CurrencyMaxLen).IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.TaxTotal).HasColumnName("tax_total").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);
        builder.Property(e => e.PaymentTermDays).HasColumnName("payment_term_days");
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(SalesOrder.NotesMaxLen);
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.StatusHistory).WithOne().HasForeignKey(h => h.SalesOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.BranchId, e.OrderNumber })
            .IsUnique()
            .HasDatabaseName("uq_sales_order_number");

        builder.HasIndex(e => new { e.SubscriberId, e.IssueDate })
            .HasDatabaseName("ix_sales_order_subscriber_issue_date");

        builder.HasIndex(e => new { e.SubscriberId, e.Status })
            .HasDatabaseName("ix_sales_order_subscriber_status");

        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId })
            .HasDatabaseName("ix_sales_order_subscriber_business_partner");
    }
}
