using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class KardexSnapshotConfiguration : IEntityTypeConfiguration<KardexSnapshot>
{
    public void Configure(EntityTypeBuilder<KardexSnapshot> builder)
    {
        builder.ToTable("kardex_snapshot");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(s => s.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(s => s.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(s => s.SnapshotDate).HasColumnName("snapshot_date").HasColumnType("date").IsRequired();
        builder.Property(s => s.BalanceQty).HasColumnName("balance_qty").HasPrecision(18, 6).IsRequired();
        builder.Property(s => s.BalanceValue).HasColumnName("balance_value").HasPrecision(18, 6).IsRequired();
        builder.Property(s => s.AverageCost).HasColumnName("average_cost").HasPrecision(18, 6).IsRequired();
        builder.Property(s => s.ComputedAt).HasColumnName("computed_at").IsRequired();

        builder.HasIndex(s => new { s.SubscriberId, s.ProductId, s.WarehouseId, s.SnapshotDate }).IsUnique().HasDatabaseName("uq_kardex_snapshot_lookup");
        builder.HasIndex(s => new { s.SubscriberId, s.SnapshotDate }).HasDatabaseName("ix_kardex_snapshot_subscriber_date");
    }
}
