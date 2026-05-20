using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseOrderBillConfiguration : IEntityTypeConfiguration<PurchaseOrderBill>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderBill> builder)
    {
        builder.ToTable("purchase_order_bill");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(v => v.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
        builder.Property(v => v.PurchBillId).HasColumnName("purch_bill_id").IsRequired();
        builder.Property(v => v.LinkedAt).HasColumnName("linked_at").IsRequired();
        builder.Property(v => v.LinkedBy).HasColumnName("linked_by").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(v => new { v.PurchaseOrderId, v.PurchBillId }).IsUnique().HasDatabaseName("uq_purchase_order_bill");
    }
}
