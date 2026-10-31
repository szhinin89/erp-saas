using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class PhysicalDocumentAuthorizationConfiguration
    : IEntityTypeConfiguration<PhysicalDocumentAuthorization>
{
    public void Configure(EntityTypeBuilder<PhysicalDocumentAuthorization> builder)
    {
        builder.ToTable(
            "physical_document_authorization",
            t =>
            {
                t.HasCheckConstraint(
                    "chk_phys_doc_auth_range",
                    "first_sequential >= 1 AND last_sequential >= first_sequential AND last_sequential <= 999999999"
                );
                t.HasCheckConstraint("chk_phys_doc_auth_dates", "expires_on >= authorized_on");
            }
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder
            .Property(x => x.DocTypeCode)
            .HasColumnName("doc_type_code")
            .HasMaxLength(5)
            .IsRequired();
        builder
            .Property(x => x.AuthorizationNumber)
            .HasColumnName("authorization_number")
            .HasMaxLength(PhysicalDocumentAuthorization.AuthorizationNumberMaxLen)
            .IsRequired();
        builder.Property(x => x.AuthorizedOn).HasColumnName("authorized_on").IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").IsRequired();
        builder.Property(x => x.FirstSequential).HasColumnName("first_sequential").IsRequired();
        builder.Property(x => x.LastSequential).HasColumnName("last_sequential").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.EmissionPointId,
                x.DocTypeCode,
            })
            .HasDatabaseName("ix_phys_doc_auth_point_doc_type");

        builder
            .HasOne<EmissionPoint>()
            .WithMany()
            .HasForeignKey(x => x.EmissionPointId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<SriDocType>()
            .WithMany()
            .HasForeignKey(x => x.DocTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
