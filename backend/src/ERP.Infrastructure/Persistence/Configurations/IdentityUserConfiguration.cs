using ERP.Domain.Access.Entities;
using ERP.Domain.Auth.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class IdentityUserConfiguration : IEntityTypeConfiguration<IdentityUser>
{
    public void Configure(EntityTypeBuilder<IdentityUser> builder)
    {
        builder.ToTable("identity_users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .IsRequired()
            .HasConversion(e => e.Value, v => new Email(v));

        builder.Property(u => u.EmailNormalized)
            .HasColumnName("email_normalized")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(u => u.UserType)
            .HasColumnName("user_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(u => u.PlatformRole)
            .HasColumnName("platform_role")
            .HasConversion<string>()
            .HasMaxLength(32);

#pragma warning disable CS0618 // SubscriberId obsoleto — requerido por EF para mapear columna legacy
        builder.Property(u => u.SubscriberId).HasColumnName("subscriber_id");
#pragma warning restore CS0618
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(64).IsRequired();
        builder.Property(u => u.RequirePasswordReset).HasColumnName("require_password_reset").IsRequired().HasDefaultValue(false);

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ux_identity_users_email");

        builder.HasIndex(u => u.EmailNormalized)
            .IsUnique()
            .HasDatabaseName("ux_identity_users_email_normalized");
    }
}
