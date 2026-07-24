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

        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.UsernameNormalized).HasColumnName("username_normalized").HasMaxLength(50).IsRequired();

        builder.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();

        // EF Core no invoca el converter para valores null — recibe/produce Email no-nulo garantizado.
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .IsRequired(false)
            .HasConversion(e => e!.Value, v => new Email(v));

        builder.Property(u => u.EmailNormalized)
            .HasColumnName("email_normalized")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();

#pragma warning disable CS0618 // tenantId obsoleto — requerido por EF para mapear columna legacy
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
#pragma warning restore CS0618
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(64).IsRequired();
        builder.Property(u => u.RequirePasswordReset).HasColumnName("require_password_reset").IsRequired().HasDefaultValue(false);

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(u => u.UsernameNormalized)
            .IsUnique()
            .HasDatabaseName("ux_identity_users_username_normalized");

        // Índices únicos parciales — email es opcional (Fase G), múltiples usuarios sin email no
        // deben violar unicidad. Postgres soporta filtro parcial vía HasFilter.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL")
            .HasDatabaseName("ux_identity_users_email");

        builder.HasIndex(u => u.EmailNormalized)
            .IsUnique()
            .HasFilter("email_normalized IS NOT NULL")
            .HasDatabaseName("ux_identity_users_email_normalized");
    }
}
