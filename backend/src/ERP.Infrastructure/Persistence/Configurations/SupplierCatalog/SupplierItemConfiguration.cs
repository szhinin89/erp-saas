using ERP.Domain.Modules.SupplierCatalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SupplierCatalog;

public sealed class SupplierItemConfiguration : IEntityTypeConfiguration<SupplierItem>
{
    public void Configure(EntityTypeBuilder<SupplierItem> builder)
    {
        builder.ToTable("supplier_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.SupplierCode).HasColumnName("supplier_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupplierName).HasColumnName("supplier_name").HasMaxLength(200);
        builder.Property(x => x.DefaultUomCode).HasColumnName("default_uom_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.LeadTimeDays).HasColumnName("lead_time_days").IsRequired();
        builder.Property(x => x.MinOrderQty).HasColumnName("min_order_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Prices)
            .WithOne()
            .HasForeignKey(nameof(SupplierItemPrice.SupplierItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SubscriberId, x.SupplierId, x.ItemId, x.VariantId })
            .IsUnique()
            .HasDatabaseName("uq_supplier_item");

        builder.HasIndex(x => new { x.SubscriberId, x.ItemId })
            .HasDatabaseName("ix_supplier_items_item");
    }
}

public sealed class SupplierItemPriceConfiguration : IEntityTypeConfiguration<SupplierItemPrice>
{
    public void Configure(EntityTypeBuilder<SupplierItemPrice> builder)
    {
        builder.ToTable("supplier_item_prices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.SupplierItemId).HasColumnName("supplier_item_id").IsRequired();
        builder.Property(x => x.MinQty).HasColumnName("min_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from").HasColumnType("date");
        builder.Property(x => x.ValidTo).HasColumnName("valid_to").HasColumnType("date");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}
