using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemImageConfiguration : IEntityTypeConfiguration<ItemImage>
{
    public void Configure(EntityTypeBuilder<ItemImage> builder)
    {
        builder.ToTable("item_images");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.StorageObjectId).HasColumnName("storage_object_id").IsRequired();
        builder.Property(x => x.AltText).HasColumnName("alt_text").HasMaxLength(200);
        builder.Property(x => x.IsMain).HasColumnName("is_main").IsRequired();
        builder.Property(x => x.IsEcommerce).HasColumnName("is_ecommerce").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => x.ItemId).HasDatabaseName("ix_item_images_item");
    }
}

public sealed class ItemUnitConversionConfiguration : IEntityTypeConfiguration<ItemUnitConversion>
{
    public void Configure(EntityTypeBuilder<ItemUnitConversion> builder)
    {
        builder.ToTable("item_unit_conversions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.FromUomCode).HasColumnName("from_uom_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.ToUomCode).HasColumnName("to_uom_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Factor).HasColumnName("factor").HasPrecision(14, 6).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => new { x.ItemId, x.FromUomCode, x.ToUomCode })
            .IsUnique()
            .HasDatabaseName("uq_item_unit_conversion");
    }
}

public sealed class ItemSubstituteConfiguration : IEntityTypeConfiguration<ItemSubstitute>
{
    public void Configure(EntityTypeBuilder<ItemSubstitute> builder)
    {
        builder.ToTable("item_substitutes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.SubstituteItemId).HasColumnName("substitute_item_id").IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(200);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => new { x.ItemId, x.SubstituteItemId })
            .IsUnique()
            .HasDatabaseName("uq_item_substitute");
    }
}

public sealed class ItemPackagingLevelConfiguration : IEntityTypeConfiguration<ItemPackagingLevel>
{
    public void Configure(EntityTypeBuilder<ItemPackagingLevel> builder)
    {
        builder.ToTable("item_packaging_levels");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Level).HasColumnName("level").IsRequired();
        builder.Property(x => x.BaseQuantity).HasColumnName("base_quantity").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.UomCode).HasColumnName("uom_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(100);
        builder.Property(x => x.Weight).HasColumnName("weight").HasPrecision(10, 3);
        builder.Property(x => x.IsBaseUnit).HasColumnName("is_base_unit").IsRequired();
        builder.Property(x => x.IsPurchaseDefault).HasColumnName("is_purchase_default").IsRequired();
        builder.Property(x => x.IsSaleDefault).HasColumnName("is_sale_default").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => new { x.ItemId, x.Level })
            .IsUnique()
            .HasDatabaseName("uq_item_packaging_level");

        builder.HasIndex(x => new { x.ItemId, x.UomCode })
            .IsUnique()
            .HasDatabaseName("uq_item_packaging_uom");
    }
}
