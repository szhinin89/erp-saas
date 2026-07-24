using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CompanyUserPreferencesConfiguration : IEntityTypeConfiguration<CompanyUserPreferences>
{
    public void Configure(EntityTypeBuilder<CompanyUserPreferences> builder)
    {
        builder.ToTable("company_user_preferences");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.CompanyUserMembershipId).HasColumnName("company_user_membership_id").IsRequired();
        builder.Property(x => x.DefaultBranchId).HasColumnName("default_branch_id");
        builder.Property(x => x.LoginMode).HasColumnName("login_mode").IsRequired();

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Relationships (FK sin navigation — entidad ligera) ──────
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:1 con la membresía — Cascade porque las preferencias no tienen sentido
        // sin la membresía que las originó (mismo criterio que CompanyUserBranch).
        builder.HasOne<CompanyUserMembership>()
            .WithMany()
            .HasForeignKey(x => x.CompanyUserMembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.DefaultBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────
        // Invariante de negocio: una única fila de preferencias por membresía
        // (relación 1:1). Ya cubre por sí sola cualquier búsqueda por
        // TenantId/CompanyId/CompanyUserMembershipId — un índice compuesto
        // adicional sobre esos tres campos sería redundante.
        builder.HasIndex(x => x.CompanyUserMembershipId)
            .IsUnique()
            .HasDatabaseName("ux_company_user_preferences_membership");

        builder.HasIndex(x => x.CompanyId)
            .HasDatabaseName("ix_company_user_preferences_company");

        // Necesario para resolver "¿qué preferencias apuntan a esta sucursal?"
        // cuando una sucursal se desactiva — mismo criterio ya usado para
        // CompanyUserBranch.BranchId.
        builder.HasIndex(x => x.DefaultBranchId)
            .HasDatabaseName("ix_company_user_preferences_default_branch");
    }
}
