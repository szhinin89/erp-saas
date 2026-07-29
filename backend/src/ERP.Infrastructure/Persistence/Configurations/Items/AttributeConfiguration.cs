using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class AttributeGroupConfiguration : IEntityTypeConfiguration<AttributeGroup>
{
    public void Configure(EntityTypeBuilder<AttributeGroup> builder)
    {
        builder.ToTable("attribute_groups");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
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
            .HasDatabaseName("uq_attribute_groups_subscriber_code");
    }
}

public sealed class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("attribute_definitions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.GroupId).HasColumnName("group_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder
            .Property(x => x.DataType)
            .HasColumnName("data_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.IsVariantAxis).HasColumnName("is_variant_axis").IsRequired();
        builder
            .Property(x => x.AllowedValues)
            .HasColumnName("allowed_values")
            .HasColumnType("jsonb");
        builder.Property(x => x.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
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
            .HasOne<AttributeGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.GroupId,
                x.Code,
            })
            .IsUnique()
            .HasDatabaseName("uq_attribute_definitions_subscriber_group_code");

        builder
            .HasIndex(x => new { x.TenantId, x.IsVariantAxis })
            .HasDatabaseName("ix_attribute_definitions_variant_axis");
    }
}
