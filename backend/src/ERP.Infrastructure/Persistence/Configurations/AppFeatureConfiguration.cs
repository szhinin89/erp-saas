using ERP.Domain.Modules.Menu.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class AppFeatureConfiguration : IEntityTypeConfiguration<AppFeature>
{
    public void Configure(EntityTypeBuilder<AppFeature> builder)
    {
        builder.ToTable("app_features");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(AppFeature.NameMaxLen).IsRequired();
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(AppFeature.IconMaxLen);
        builder.Property(x => x.Path).HasColumnName("path").HasMaxLength(AppFeature.PathMaxLen);
        builder.Property(x => x.Permission).HasColumnName("permission").HasMaxLength(AppFeature.PermissionMaxLen).IsRequired();
        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsVisibleInMenu).HasColumnName("is_visible_in_menu").IsRequired();
        builder.Property(x => x.IsSuperAdmin).HasColumnName("is_super_admin").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(x => x.Permission).IsUnique().HasDatabaseName("uq_app_features_permission");
        builder.HasIndex(x => x.ParentId).HasDatabaseName("ix_app_features_parent_id");

        builder.HasOne<AppFeature>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
