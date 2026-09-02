using ERP.Domain.Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class GlobalUserRoleConfiguration : IEntityTypeConfiguration<GlobalUserRole>
{
    public void Configure(EntityTypeBuilder<GlobalUserRole> builder)
    {
        builder.ToTable("global_user_roles", "access");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        builder
            .Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder
            .Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.UserId, x.Role })
            .IsUnique()
            .HasDatabaseName("ux_global_user_roles_user_role");

        builder
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_global_user_roles_identity_users_user_id");
    }
}
