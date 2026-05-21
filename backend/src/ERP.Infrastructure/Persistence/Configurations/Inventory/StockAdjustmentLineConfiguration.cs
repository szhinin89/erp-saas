using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class StockAdjustmentLineConfiguration : IEntityTypeConfiguration<StockAdjustmentLine>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentLine> builder)
    {
        builder.ToTable("stock_adjustment_line");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.StockAdjustmentId).HasColumnName("stock_adjustment_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.SystemQuantity).HasColumnName("system_quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.PhysicalQuantity).HasColumnName("physical_quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.AdjustmentQuantity).HasColumnName("adjustment_quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue((short)0);

        builder.HasOne(x => x.StockAdjustment)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.StockAdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StockAdjustmentId).HasDatabaseName("ix_stock_adjustment_line_adjustment");
        builder.HasIndex(x => new { x.StockAdjustmentId, x.SortOrder }).HasDatabaseName("ix_stock_adjustment_line_adjustment_sort");
    }
}
