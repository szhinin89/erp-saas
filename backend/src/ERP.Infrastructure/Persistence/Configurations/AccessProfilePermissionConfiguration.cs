using ERP.Domain.Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AccessProfilePermissionConfiguration : IEntityTypeConfiguration<AccessProfilePermission>
{
    public void Configure(EntityTypeBuilder<AccessProfilePermission> builder)
    {
        builder.ToTable("access_profile_permissions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(p => p.PermissionKey).HasColumnName("permission_key").HasMaxLength(180).IsRequired();
        builder.Property(p => p.IsAllowed).HasColumnName("is_allowed").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(p => new { p.TenantId, p.ProfileId, p.PermissionKey })
            .IsUnique()
            .HasDatabaseName("ux_access_profile_permissions_tenant_profile_key");

        builder.HasIndex(p => new { p.TenantId, p.PermissionKey })
            .HasDatabaseName("ix_access_profile_permissions_tenant_key");
    }
}

