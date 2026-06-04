using ERP.Domain.Modules.DigitalItems.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.DigitalItems;

public sealed class DigitalItemConfiguration : IEntityTypeConfiguration<DigitalItem>
{
    public void Configure(EntityTypeBuilder<DigitalItem> builder)
    {
        builder.ToTable("digital_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.LicenseType)
            .HasColumnName("license_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.DeliveryMethod)
            .HasColumnName("delivery_method")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.MaxDownloads).HasColumnName("max_downloads");
        builder.Property(x => x.ValidityDays).HasColumnName("validity_days");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Deliverables)
            .WithOne()
            .HasForeignKey(nameof(DigitalDeliverable.DigitalItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ItemId)
            .IsUnique()
            .HasDatabaseName("uq_digital_item");
    }
}

public sealed class DigitalDeliverableConfiguration : IEntityTypeConfiguration<DigitalDeliverable>
{
    public void Configure(EntityTypeBuilder<DigitalDeliverable> builder)
    {
        builder.ToTable("digital_deliverables");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.DigitalItemId).HasColumnName("digital_item_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.StorageObjectId).HasColumnName("storage_object_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(50);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}
