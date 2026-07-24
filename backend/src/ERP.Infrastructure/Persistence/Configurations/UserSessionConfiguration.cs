using ERP.Domain.Access.Entities;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.IdentityUserId).HasColumnName("identity_user_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.TerminalId).HasColumnName("terminal_id")
            .HasMaxLength(UserSession.TerminalIdMaxLen).IsRequired();
        builder.Property(x => x.RefreshTokenId).HasColumnName("refresh_token_id");

        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion<int>().IsRequired();

        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.ClosedReason).HasColumnName("closed_reason").HasMaxLength(200);

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Concurrency token (PostgreSQL xid) ──────────────────────
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Computed properties (NOT persisted) ─────────────────────
        builder.Ignore(x => x.IsActive);

        // ── Relationships (FK sin navigation — entidad ligera) ──────
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.IdentityUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unidireccional: RefreshToken (Auth) nunca referencia UserSession (Access).
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(x => x.RefreshTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────
        // Invariante dura: máximo una sesión Active por (tenant, empresa, usuario).
        // Status.Active = 1 (ver UserSessionStatus).
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IdentityUserId })
            .IsUnique()
            .HasFilter("status = 1")
            .HasDatabaseName("ux_user_sessions_active_per_company");

        builder.HasIndex(x => new { x.IdentityUserId, x.TenantId, x.Status })
            .HasDatabaseName("ix_user_sessions_identity_user_tenant_status");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_user_sessions_company");
    }
}
