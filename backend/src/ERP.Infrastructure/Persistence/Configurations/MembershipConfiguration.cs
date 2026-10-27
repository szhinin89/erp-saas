using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class CompanyUserMembershipConfiguration : IEntityTypeConfiguration<CompanyUserMembership>
{
    public void Configure(EntityTypeBuilder<CompanyUserMembership> builder)
    {
        builder.ToTable("company_user_memberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(m => m.IdentityUserId).HasColumnName("identity_user_id").IsRequired();
        builder.Property(m => m.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
        builder.Property(m => m.ProfileId).HasColumnName("profile_id");
        builder.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder
            .HasIndex(m => new { m.CompanyId, m.IdentityUserId })
            .IsUnique()
            .HasDatabaseName("ux_company_user_memberships_company_identity_user");

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_user_memberships_company_company_id");

        // SEC-05: FK compuesta — el perfil asignado debe pertenecer a la misma empresa de la
        // membership. Con profile_id NULL (Admin sin perfil) la restricción no aplica.
        builder
            .HasOne<AccessProfile>()
            .WithMany()
            .HasForeignKey(m => new { m.CompanyId, m.ProfileId })
            .HasPrincipalKey(p => new { p.CompanyId, p.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_user_memberships_access_profiles_company_profile");
    }
}
