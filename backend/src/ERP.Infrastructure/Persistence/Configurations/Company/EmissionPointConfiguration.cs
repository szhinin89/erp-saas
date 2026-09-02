using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class EmissionPointConfiguration : IEntityTypeConfiguration<EmissionPoint>
{
    public void Configure(EntityTypeBuilder<EmissionPoint> builder)
    {
        builder.ToTable("emission_point");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id").IsRequired();

        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(EmissionPoint.CodeMaxLen)
            .IsFixedLength()
            .IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(EmissionPoint.NameMaxLen);
        builder
            .Property(x => x.EmissionType)
            .HasColumnName("emission_type")
            .IsRequired()
            .HasDefaultValue(EmissionType.Electronic)
            .HasSentinel((EmissionType)0);
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();

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

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_emission_point_tenant_id");
        builder
            .HasIndex(x => new { x.EstablishmentId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_ep_code");

        // CONFIG-FOUNDATION-P0-01: garantiza en DB que solo exista un punto de emisión default
        // por establecimiento (Fase 10 de docs/architecture/configuration-engine-target-architecture.md).
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.EstablishmentId,
            })
            .IsUnique()
            .HasDatabaseName("uq_emission_point_establishment_default")
            .HasFilter("is_default = true");

        builder
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.Establishment)
            .WithMany(x => x.EmissionPoints)
            .HasForeignKey(x => x.EstablishmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
