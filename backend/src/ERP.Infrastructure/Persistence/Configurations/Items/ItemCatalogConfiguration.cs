using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(Brand.MaxCodeLength)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(Brand.MaxNameLength)
            .IsRequired();
        builder
            .Property(x => x.Manufacturer)
            .HasColumnName("manufacturer")
            .HasMaxLength(Brand.MaxManufacturerLength);
        builder
            .Property(x => x.CountryOfOrigin)
            .HasColumnName("country_of_origin")
            .HasMaxLength(Brand.MaxCountryLength);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("ix_brands_subscriber_code");
    }
}
