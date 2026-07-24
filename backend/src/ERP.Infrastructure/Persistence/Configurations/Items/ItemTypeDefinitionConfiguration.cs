using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemTypeDefinitionConfiguration : IEntityTypeConfiguration<ItemTypeDefinition>
{
    public void Configure(EntityTypeBuilder<ItemTypeDefinition> builder)
    {
        builder.ToTable("item_types");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(ItemTypeDefinition.MaxCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(ItemTypeDefinition.MaxNameLength).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_item_types_tenant_code");

        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("ix_item_types_tenant_active");
    }
}
