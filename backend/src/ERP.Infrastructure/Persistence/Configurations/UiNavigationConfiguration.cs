using ERP.Domain.Navigation.Entities;
using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class UiNavGroupConfiguration : IEntityTypeConfiguration<UiNavGroup>
{
    public void Configure(EntityTypeBuilder<UiNavGroup> builder)
    {
        builder.ToTable("ui_nav_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(32).IsRequired();
        builder.Property(x => x.LabelKey).HasColumnName("label_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.ModuleKey).HasColumnName("module_key").HasMaxLength(64);
        builder.Property(x => x.RolesCsv).HasColumnName("roles_csv").HasMaxLength(200);
        builder.Property(x => x.RequirePlatformPanel).HasColumnName("require_superadmin_panel");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
    }
}

public sealed class UiNavItemConfiguration : IEntityTypeConfiguration<UiNavItem>
{
    public void Configure(EntityTypeBuilder<UiNavItem> builder)
    {
        builder.ToTable("ui_nav_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).HasColumnName("group_id").IsRequired();
        builder.HasIndex(x => new { x.GroupId, x.RoutePath });
        builder.Property(x => x.ParentItemId).HasColumnName("parent_item_id");
        builder.HasIndex(x => new { x.GroupId, x.ParentItemId, x.SortOrder });
        builder.Property(x => x.RoutePath).HasColumnName("route_path").HasMaxLength(512).IsRequired();
        builder.Property(x => x.LabelKey).HasColumnName("label_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayLabel).HasColumnName("display_label").HasMaxLength(200);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.ModuleKey).HasColumnName("module_key").HasMaxLength(64);
        builder.Property(x => x.PermissionKey).HasColumnName("permission_key").HasMaxLength(128);
        builder.Property(x => x.PermissionKeysAnyJson).HasColumnName("permission_keys_any_json").HasMaxLength(2000);
        builder.Property(x => x.RolesCsv).HasColumnName("roles_csv").HasMaxLength(200);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.PlatformFeatureId).HasColumnName("saas_feature_definition_id");
        builder.HasIndex(x => x.PlatformFeatureId);

        builder.HasOne<PlatformFeature>()
            .WithMany()
            .HasForeignKey(x => x.PlatformFeatureId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<UiNavGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UiNavItem>()
            .WithMany()
            .HasForeignKey(x => x.ParentItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
