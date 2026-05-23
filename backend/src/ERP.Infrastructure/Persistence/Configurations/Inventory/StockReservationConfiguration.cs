using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(r => r.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(r => r.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.Quantity).HasColumnName("quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(r => r.IsExpired);

        builder.HasIndex(r => new { r.SubscriberId, r.ProductId, r.WarehouseId, r.Status })
            .HasDatabaseName("ix_stock_reservations_product_warehouse_status");

        builder.HasIndex(r => new { r.SubscriberId, r.OrderId })
            .HasFilter("order_id IS NOT NULL")
            .HasDatabaseName("ix_stock_reservations_order");

        builder.HasIndex(r => new { r.SubscriberId, r.ExpiresAt, r.Status })
            .HasDatabaseName("ix_stock_reservations_expiry");
    }
}
