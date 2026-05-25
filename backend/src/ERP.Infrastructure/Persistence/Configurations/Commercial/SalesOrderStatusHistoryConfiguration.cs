using ERP.Domain.Modules.Commercial.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Commercial;

public sealed class SalesOrderStatusHistoryConfiguration : IEntityTypeConfiguration<SalesOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<SalesOrderStatusHistory> builder)
    {
        builder.ToTable("sales_order_status_history");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SalesOrderId).HasColumnName("sales_order_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.FromStatus).HasColumnName("from_status").HasMaxLength(SalesOrderStatusHistory.StatusMaxLen);
        builder.Property(e => e.ToStatus).HasColumnName("to_status").HasMaxLength(SalesOrderStatusHistory.StatusMaxLen).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(e => e.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();

        builder.HasIndex(e => new { e.SubscriberId, e.SalesOrderId, e.ChangedAt })
            .HasDatabaseName("ix_sales_order_status_history_order");
    }
}
