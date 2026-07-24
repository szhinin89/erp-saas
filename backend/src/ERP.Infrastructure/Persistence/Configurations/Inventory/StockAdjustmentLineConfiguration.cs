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
        builder.Property(x => x.StockAdjustmentId).HasColumnName("stock_adjustment_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.SystemQuantity).HasColumnName("system_quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.PhysicalQuantity).HasColumnName("physical_quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.AdjustmentQuantity).HasColumnName("adjustment_quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost")
            .HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(200);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.HasIndex(x => x.StockAdjustmentId)
            .HasDatabaseName("ix_stock_adjustment_lines_adjustment");
    }
}
