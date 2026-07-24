using ERP.Domain.Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AccessProfileConfiguration : IEntityTypeConfiguration<AccessProfile>
{
    public void Configure(EntityTypeBuilder<AccessProfile> builder)
    {
        builder.ToTable("access_profiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(400);
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(p => new { p.TenantId, p.Name })
            .IsUnique()
            .HasDatabaseName("ux_access_profiles_subscriber_name");
    }
}

