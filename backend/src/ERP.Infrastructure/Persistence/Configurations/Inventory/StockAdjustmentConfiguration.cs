using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustment");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").IsRequired();
        builder.Property(e => e.AdjustmentNumber).HasColumnName("adjustment_number").HasMaxLength(StockAdjustment.NumberMaxLen).IsRequired();
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(e => e.WarehouseName).HasColumnName("warehouse_name").HasMaxLength(StockAdjustment.NameSnapshotMaxLen).IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.ProductName).HasColumnName("product_name").HasMaxLength(StockAdjustment.NameSnapshotMaxLen).IsRequired();
        builder.Property(e => e.AdjustmentQty).HasColumnName("adjustment_qty").HasPrecision(18, 6).IsRequired();
        builder.Property(e => e.AdjustmentType).HasColumnName("adjustment_type").HasMaxLength(StockAdjustment.AdjustmentTypeMaxLen).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(StockAdjustment.ReasonMaxLen).IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(StockAdjustment.NotesMaxLen);
        builder.Property(e => e.AdjustmentDate).HasColumnName("adjustment_date").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(StockAdjustment.StatusMaxLen).IsRequired();
        builder.Property(e => e.ExecutedAt).HasColumnName("executed_at");
        builder.Property(e => e.ExecutedBy).HasColumnName("executed_by");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.SubscriberId, e.AdjustmentNumber }).IsUnique().HasDatabaseName("uq_stock_adjustment_number");
        builder.HasIndex(e => new { e.SubscriberId, e.Status }).HasDatabaseName("ix_stock_adjustment_subscriber_status");
        builder.HasIndex(e => new { e.SubscriberId, e.WarehouseId }).HasDatabaseName("ix_stock_adjustment_subscriber_warehouse");
    }
}
