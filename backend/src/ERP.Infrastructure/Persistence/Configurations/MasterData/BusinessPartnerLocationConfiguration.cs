using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class BusinessPartnerLocationConfiguration : IEntityTypeConfiguration<BusinessPartnerLocation>
{
    public void Configure(EntityTypeBuilder<BusinessPartnerLocation> builder)
    {
        builder.ToTable("master_bp_locations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(BusinessPartnerLocation.NameMaxLen)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Address)
            .HasColumnName("address")
            .HasMaxLength(BusinessPartnerLocation.AddressMaxLen)
            .IsRequired();

        builder.Property(e => e.ProvinceCode)
            .HasColumnName("province_code")
            .HasMaxLength(BusinessPartnerLocation.GeoCodeMaxLen);

        builder.Property(e => e.CantonCode)
            .HasColumnName("canton_code")
            .HasMaxLength(BusinessPartnerLocation.GeoCodeMaxLen);

        builder.Property(e => e.ParishCode)
            .HasColumnName("parish_code")
            .HasMaxLength(BusinessPartnerLocation.GeoCodeMaxLen);

        builder.Property(e => e.Phone)
            .HasColumnName("phone")
            .HasMaxLength(BusinessPartnerLocation.PhoneMaxLen);

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasMaxLength(BusinessPartnerLocation.EmailMaxLen);

        builder.Property(e => e.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // ── Índices ───────────────────────────────────────────────────────────
        builder.HasIndex(e => new { e.SubscriberId, e.CompanyId, e.BusinessPartnerId })
            .HasDatabaseName("ix_mbpl_subscriber_company_bp");

        // Una sola ubicación primaria por (company, BP) — partial unique index via SQL raw
        // EF no soporta partial indexes en OnModelCreating; se crea en migration

        // FK a global.geo_provinces (no cascade — es referencia inmutable)
        builder.HasOne<ERP.Domain.Geography.Entities.GeoProvince>()
            .WithMany()
            .HasForeignKey(e => e.ProvinceCode)
            .HasPrincipalKey(p => p.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ERP.Domain.Geography.Entities.GeoCanton>()
            .WithMany()
            .HasForeignKey(e => e.CantonCode)
            .HasPrincipalKey(c => c.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ERP.Domain.Geography.Entities.GeoParish>()
            .WithMany()
            .HasForeignKey(e => e.ParishCode)
            .HasPrincipalKey(p => p.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
