using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockAdjustmentLineConfiguration : IEntityTypeConfiguration<StockAdjustmentLine>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentLine> builder)
    {
        builder.ToTable("stock_adjustment_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.StockAdjustmentId)
            .HasColumnName("stock_adjustment_id")
            .IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder
            .Property(x => x.ItemName)
            .HasColumnName("item_name")
            .HasMaxLength(StockAdjustmentLine.ItemNameMaxLen)
            .IsRequired();
        builder.Property(x => x.PackagingLevelId).HasColumnName("packaging_level_id");
        builder
            .Property(x => x.UomCode)
            .HasColumnName("uom_code")
            .HasMaxLength(StockAdjustmentLine.UomCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.BaseUomCode)
            .HasColumnName("base_uom_code")
            .HasMaxLength(StockAdjustmentLine.UomCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.ConversionFactor)
            .HasColumnName("conversion_factor")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder
            .Property(x => x.QuantityInBaseUom)
            .HasColumnName("quantity_in_base_uom")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder
            .Property(x => x.UnitCostBase)
            .HasColumnName("unit_cost_base")
            .HasColumnType("numeric(18,6)");
        builder.Property(x => x.TotalCost).HasColumnName("total_cost").HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.CurrentStockBefore)
            .HasColumnName("current_stock_before")
            .HasColumnType("numeric(18,4)");
        builder
            .Property(x => x.CurrentStockAfter)
            .HasColumnName("current_stock_after")
            .HasColumnType("numeric(18,4)");
        builder
            .Property(x => x.LineNotes)
            .HasColumnName("line_notes")
            .HasMaxLength(StockAdjustmentLine.LineNotesMaxLen);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder
            .HasIndex(x => x.StockAdjustmentId)
            .HasDatabaseName("ix_stock_adjustment_lines_adjustment");
        builder.HasIndex(x => x.ItemId).HasDatabaseName("ix_stock_adjustment_lines_item");
    }
}
