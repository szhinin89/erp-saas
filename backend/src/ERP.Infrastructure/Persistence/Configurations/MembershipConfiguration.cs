using ERP.Domain.Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.IdentityUserId).HasColumnName("identity_user_id").IsRequired();
        builder.Property(m => m.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
        builder.Property(m => m.ProfileId).HasColumnName("profile_id");
        builder.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(m => new { m.TenantId, m.IdentityUserId })
            .IsUnique()
            .HasDatabaseName("ux_memberships_tenant_identity_user");
    }
}

