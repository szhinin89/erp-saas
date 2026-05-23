using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_order");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").IsRequired();
        builder.Property(e => e.OrderNumber).HasColumnName("order_number").HasMaxLength(PurchaseOrder.NumberMaxLen).IsRequired();
        builder.Property(e => e.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.RequiredDate).HasColumnName("required_date").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(PurchaseOrder.StatusMaxLen).IsRequired();
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(PurchaseOrder.CurrencyMaxLen).IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.TaxTotal).HasColumnName("tax_total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(PurchaseOrder.NotesMaxLen);
        builder.Property(e => e.DeliveryAddress).HasColumnName("delivery_address").HasMaxLength(PurchaseOrder.AddressMaxLen);
        builder.Property(e => e.TargetWarehouseId).HasColumnName("target_warehouse_id");
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.ApprovedBy).HasColumnName("approved_by");
        builder.Property(e => e.ClosedAt).HasColumnName("closed_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.OrderNumber }).IsUnique().HasDatabaseName("uq_purchase_order_number");
        builder.HasIndex(e => new { e.SubscriberId, e.Status }).HasDatabaseName("ix_purchase_order_subscriber_status");
        builder.HasIndex(e => new { e.SubscriberId, e.SupplierId }).HasDatabaseName("ix_purchase_order_subscriber_supplier");
    }
}
