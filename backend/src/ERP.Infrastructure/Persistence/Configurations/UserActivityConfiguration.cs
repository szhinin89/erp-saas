using ERP.Domain.Audit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.ToTable("user_activity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.UserEmail).HasColumnName("user_email").HasMaxLength(254);
        builder.Property(x => x.UserFullName).HasColumnName("user_full_name").HasMaxLength(254);

        builder.Property(x => x.Module).HasColumnName("module").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(80);
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(800);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt })
            .HasDatabaseName("ix_user_activity_tenant_user_created_at");
        builder.HasIndex(x => new { x.TenantId, x.Module, x.CreatedAt })
            .HasDatabaseName("ix_user_activity_tenant_module_created_at");
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.CreatedAt })
            .HasDatabaseName("ix_user_activity_tenant_entity_created_at");
    }
}

