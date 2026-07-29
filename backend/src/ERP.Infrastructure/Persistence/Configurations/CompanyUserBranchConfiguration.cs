using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CompanyUserBranchConfiguration : IEntityTypeConfiguration<CompanyUserBranch>
{
    public void Configure(EntityTypeBuilder<CompanyUserBranch> builder)
    {
        builder.ToTable("company_user_branches");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(x => x.CompanyUserMembershipId)
            .HasColumnName("company_user_membership_id")
            .IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Relationships (FK sin navigation — entidad ligera) ──────
        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<CompanyUserMembership>()
            .WithMany()
            .HasForeignKey(x => x.CompanyUserMembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────
        // Evita autorizaciones duplicadas para la misma membresía + sucursal.
        builder
            .HasIndex(x => new { x.CompanyUserMembershipId, x.BranchId })
            .IsUnique()
            .HasDatabaseName("ux_company_user_branches_membership_branch");

        builder.HasIndex(x => x.CompanyId).HasDatabaseName("ix_company_user_branches_company");

        builder.HasIndex(x => x.BranchId).HasDatabaseName("ix_company_user_branches_branch");
    }
}
