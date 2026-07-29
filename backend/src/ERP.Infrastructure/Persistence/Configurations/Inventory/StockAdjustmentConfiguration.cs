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
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder
            .Property(x => x.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(StockAdjustment.NameSnapshotMaxLen)
            .IsRequired();
        builder
            .Property(x => x.AdjustmentQty)
            .HasColumnName("adjustment_qty")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder
            .Property(x => x.AdjustmentType)
            .HasColumnName("adjustment_type")
            .HasMaxLength(StockAdjustment.AdjustmentTypeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(StockAdjustment.ReasonMaxLen)
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
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_stock_adjustments_tenant_company");
        builder
            .HasIndex(x => new { x.TenantId, x.AdjustmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_stock_adjustments_tenant_number");
    }
}
