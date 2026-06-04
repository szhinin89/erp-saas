using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lots");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.LotNumber).HasColumnName("lot_number").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.ManufactureDate).HasColumnName("manufacture_date").HasColumnType("date");
        builder.Property(x => x.ExpirationDate).HasColumnName("expiration_date").HasColumnType("date");
        builder.Property(x => x.InitialQty).HasColumnName("initial_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.CurrentQty).HasColumnName("current_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
        builder.Property(x => x.ReceiptLineId).HasColumnName("receipt_line_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId, x.ItemId, x.LotNumber })
            .IsUnique()
            .HasDatabaseName("uq_lot_number");

        builder.HasIndex(x => new { x.SubscriberId, x.ItemId })
            .HasDatabaseName("ix_lots_subscriber_item");

        // Partial index for expiration alerts — only active lots
        builder.HasIndex(x => x.ExpirationDate)
            .HasFilter("expiration_date IS NOT NULL AND status = 'Active'")
            .HasDatabaseName("ix_lots_expiration_active");
    }
}
