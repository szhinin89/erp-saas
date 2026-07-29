using ERP.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder
            .Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.UserKind).HasColumnName("user_kind").HasMaxLength(20).IsRequired();

        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(e => e.Used).HasColumnName("used").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder
            .HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_password_reset_tokens_hash");

        builder
            .HasIndex(e => new
            {
                e.UserId,
                e.UserKind,
                e.TenantId,
            })
            .HasDatabaseName("ix_password_reset_tokens_user");
    }
}
