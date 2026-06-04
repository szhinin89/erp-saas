using ERP.Domain.Modules.Kits.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Kits;

public sealed class KitConfiguration : IEntityTypeConfiguration<Kit>
{
    public void Configure(EntityTypeBuilder<Kit> builder)
    {
        builder.ToTable("kits");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.KitType)
            .HasColumnName("kit_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(nameof(KitLine.KitId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ItemId)
            .IsUnique()
            .HasDatabaseName("uq_kit_item");
    }
}

public sealed class KitLineConfiguration : IEntityTypeConfiguration<KitLine>
{
    public void Configure(EntityTypeBuilder<KitLine> builder)
    {
        builder.ToTable("kit_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.KitId).HasColumnName("kit_id").IsRequired();
        builder.Property(x => x.ComponentItemId).HasColumnName("component_item_id").IsRequired();
        builder.Property(x => x.ComponentVariantId).HasColumnName("component_variant_id");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.UomCode).HasColumnName("uom_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.IsOptional).HasColumnName("is_optional").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => new { x.KitId, x.ComponentItemId, x.ComponentVariantId })
            .IsUnique()
            .HasDatabaseName("uq_kit_component");
    }
}
