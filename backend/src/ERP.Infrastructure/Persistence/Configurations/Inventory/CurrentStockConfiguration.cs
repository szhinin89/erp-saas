using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class CurrentStockConfiguration : IEntityTypeConfiguration<CurrentStock>
{
    public void Configure(EntityTypeBuilder<CurrentStock> builder)
    {
        builder.ToTable("current_stock");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(s => s.CompanyId).HasColumnName("company_id");
        builder.Property(s => s.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(s => s.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(s => s.Quantity).HasColumnName("quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(s => s.ReservedQuantity).HasColumnName("reserved_quantity").HasPrecision(18, 6).IsRequired();
        builder.Ignore(s => s.AvailableQuantity);
        builder.Ignore(s => s.AverageCost);
        builder.Property(s => s.TotalStockValue).HasColumnName("total_stock_value").HasPrecision(18, 6).IsRequired();
        builder.Property(s => s.LastUpdatedAt).HasColumnName("last_updated_at").IsRequired();
        builder.Property(s => s.RowVersion).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(s => new { s.SubscriberId, s.ProductId, s.WarehouseId }).IsUnique().HasDatabaseName("uq_current_stock_subscriber_product_warehouse");
        builder.HasIndex(s => new { s.SubscriberId, s.CompanyId }).HasDatabaseName("ix_current_stock_subscriber_company");
    }
}
