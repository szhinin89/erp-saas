using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.Sequential).HasColumnName("sequential").IsRequired();
        builder
            .Property(x => x.AdjustmentNumber)
            .HasColumnName("adjustment_number")
            .HasMaxLength(StockAdjustment.NumberMaxLen)
            .IsRequired();
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder
            .Property(x => x.WarehouseName)
            .HasColumnName("warehouse_name")
            .HasMaxLength(StockAdjustment.NameSnapshotMaxLen)
            .IsRequired();
        builder.Property(x => x.ReasonId).HasColumnName("reason_id").IsRequired();
        builder
            .Property(x => x.MovementType)
            .HasColumnName("movement_type")
            .HasMaxLength(StockAdjustment.MovementTypeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(StockAdjustment.NotesMaxLen);
        builder.Property(x => x.AdjustmentDate).HasColumnName("adjustment_date").IsRequired();
        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(StockAdjustment.StatusMaxLen)
            .IsRequired();
        builder.Property(x => x.ExecutedAt).HasColumnName("executed_at");
        builder.Property(x => x.ExecutedBy).HasColumnName("executed_by");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        builder
            .Property(x => x.CancelledReason)
            .HasColumnName("cancelled_reason")
            .HasMaxLength(StockAdjustment.CancelledReasonMaxLen);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasMany(x => x.Lines)
            .WithOne(x => x.StockAdjustment)
            .HasForeignKey(x => x.StockAdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ERP.Domain.Modules.Inventory.Entities.InventoryAdjustmentReason>()
            .WithMany()
            .HasForeignKey(x => x.ReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_stock_adjustments_tenant_company");
        builder
            .HasIndex(x => new { x.TenantId, x.AdjustmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_stock_adjustments_tenant_number");
        builder.HasIndex(x => x.ReasonId).HasDatabaseName("ix_stock_adjustments_reason");
        builder.HasIndex(x => x.WarehouseId).HasDatabaseName("ix_stock_adjustments_warehouse");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_stock_adjustments_status");
    }
}
