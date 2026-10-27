using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AccessProfileConfiguration : IEntityTypeConfiguration<AccessProfile>
{
    public void Configure(EntityTypeBuilder<AccessProfile> builder)
    {
        builder.ToTable("access_profiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(400);
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_access_profiles_company_company_id");

        // SEC-05: principal de la FK compuesta (company_id, profile_id) de
        // company_user_memberships — una membership solo referencia perfiles de su empresa.
        builder
            .HasAlternateKey(p => new { p.CompanyId, p.Id })
            .HasName("ak_access_profiles_company_id");

        // SEC-05: el nombre es único por empresa; dos empresas pueden repetirlo.
        builder
            .HasIndex(p => new
            {
                p.TenantId,
                p.CompanyId,
                p.Name,
            })
            .IsUnique()
            .HasDatabaseName("ux_access_profiles_tenant_company_name");
    }
}
