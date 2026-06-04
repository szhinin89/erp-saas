using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemVariantConfiguration : IEntityTypeConfiguration<ItemVariant>
{
    public void Configure(EntityTypeBuilder<ItemVariant> builder)
    {
        builder.ToTable("item_variants");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();

        builder.Property(x => x.SKU).HasColumnName("sku").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Attributes)
            .WithOne()
            .HasForeignKey(nameof(ItemVariantAttribute.VariantId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Barcodes)
            .WithOne()
            .HasForeignKey(nameof(ItemVariantBarcode.VariantId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ItemId, x.SKU })
            .IsUnique()
            .HasDatabaseName("uq_item_variants_item_sku");

        builder.HasIndex(x => x.ItemId)
            .HasDatabaseName("ix_item_variants_item");
    }
}

public sealed class ItemVariantAttributeConfiguration : IEntityTypeConfiguration<ItemVariantAttribute>
{
    public void Configure(EntityTypeBuilder<ItemVariantAttribute> builder)
    {
        builder.ToTable("item_variant_attributes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
        builder.Property(x => x.AttributeDefinitionId).HasColumnName("attribute_definition_id").IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.VariantId, x.AttributeDefinitionId })
            .IsUnique()
            .HasDatabaseName("uq_item_variant_attribute");
    }
}

public sealed class ItemVariantBarcodeConfiguration : IEntityTypeConfiguration<ItemVariantBarcode>
{
    public void Configure(EntityTypeBuilder<ItemVariantBarcode> builder)
    {
        builder.ToTable("item_variant_barcodes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.BarcodeType)
            .HasColumnName("barcode_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => new { x.ItemId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_item_variant_barcode_code");

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_item_variant_barcodes_code");
    }
}
