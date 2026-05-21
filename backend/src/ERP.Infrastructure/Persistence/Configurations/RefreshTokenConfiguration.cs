using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Auth.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");

        builder.Property(e => e.UserType)
            .HasColumnName("user_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(e => e.IsRevoked).HasColumnName("is_revoked").IsRequired();
        builder.Property(e => e.RevokedAt).HasColumnName("revoked_at");

        builder.Property(e => e.ReplacedByHash)
            .HasColumnName("replaced_by_hash")
            .HasMaxLength(128);

        builder.Property(e => e.ReasonRevoked)
            .HasColumnName("reason_revoked")
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(e => e.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.Property(e => e.ParentTokenId)
            .HasColumnName("parent_token_id");

        builder.Property(e => e.RotationDepth)
            .HasColumnName("rotation_depth")
            .IsRequired();

        // Búsqueda principal por hash (único para prevenir duplicados)
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_hash");

        // Índice para revocación por familia (replay sospechoso)
        builder.HasIndex(e => e.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        // Índice para revocación masiva por usuario/tenant
        builder.HasIndex(e => new { e.UserId, e.SubscriberId })
            .HasDatabaseName("ix_refresh_tokens_user_subscriber");

        // Índice para limpieza de tokens expirados
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");
    }
}
