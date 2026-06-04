using ERP.Domain.Geography.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para BusinessPartnerLocation.
///
/// CAMBIOS respecto al modelo anterior:
///   - Eliminado: company_id (scope cambiado a subscriber-only). ADR-BP-02 (Fase 4).
///   - Agregado: location_purpose (int bitmask — [Flags] LocationPurpose)
///   - Eliminado: address, province_code, canton_code, parish_code como columnas directas.
///   - Agregado: PhysicalAddress OwnsOne (aplanado en las mismas columnas renombradas).
///   - FK compuesto (business_partner_id, subscriber_id) → BP(id, subscriber_id). ADR-BP-13.
///   - OtherDescription: obligatorio cuando LocationType = Other. Validado en dominio.
///
/// ÍNDICE PARCIAL ÚNICO is_primary:
///   EF Core expresa el filtro via HasFilter. La migración lo genera como:
///   CREATE UNIQUE INDEX uq_bpl_primary ON master_bp_locations
///   (subscriber_id, business_partner_id) WHERE is_primary = true AND is_active = true;
///
/// QUERY FILTER:
///   ISubscriberScopedEntity → subscriber fail-closed (EnterpriseQueryFilterConfigurator).
///   No hay company_id → no se aplica company filter (correcto por diseño).
/// </summary>
public sealed class BusinessPartnerLocationConfiguration : IEntityTypeConfiguration<BusinessPartnerLocation>
{
    public void Configure(EntityTypeBuilder<BusinessPartnerLocation> builder)
    {
        builder.ToTable("master_bp_locations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();

        builder.Property(e => e.Name)
               .HasColumnName("name")
               .HasMaxLength(BusinessPartnerLocation.NameMaxLen)
               .IsRequired();

        builder.Property(e => e.Type)
               .HasColumnName("location_type")
               .HasConversion<short>()
               .IsRequired();

        // LocationPurpose: [Flags] enum almacenado como int bitmask.
        // Permite Billing | Delivery | Fiscal en una sola columna.
        builder.Property(e => e.Purpose)
               .HasColumnName("location_purpose")
               .HasConversion<int>()
               .IsRequired()
               .HasDefaultValue(LocationPurpose.None);

        builder.Property(e => e.OtherDescription)
               .HasColumnName("other_description")
               .HasMaxLength(BusinessPartnerLocation.OtherDescriptionMaxLen);

        // ── PhysicalAddress VO (owned, columnas aplanadas en master_bp_locations) ──
        builder.OwnsOne(e => e.Address, addr =>
        {
            addr.Property(a => a.AddressLine)
                .HasColumnName("address_line")
                .HasMaxLength(PhysicalAddress.AddressLineMaxLen)
                .IsRequired();

            addr.Property(a => a.ProvinceCode)
                .HasColumnName("province_code")
                .HasMaxLength(PhysicalAddress.ProvinceCodeLen)
                .IsFixedLength();

            addr.Property(a => a.CantonCode)
                .HasColumnName("canton_code")
                .HasMaxLength(PhysicalAddress.CantonCodeLen)
                .IsFixedLength();

            addr.Property(a => a.ParishCode)
                .HasColumnName("parish_code")
                .HasMaxLength(PhysicalAddress.ParishCodeLen)
                .IsFixedLength();

            // FKs a catálogos geográficos INEC (tablas global.geo_*)
            // OnDelete Restrict: los catálogos son inmutables — nunca se eliminan
            addr.HasOne<GeoProvince>()
                .WithMany()
                .HasForeignKey(a => a.ProvinceCode)
                .HasPrincipalKey(p => p.Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_bpl_province");

            addr.HasOne<GeoCanton>()
                .WithMany()
                .HasForeignKey(a => a.CantonCode)
                .HasPrincipalKey(c => c.Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_bpl_canton");

            addr.HasOne<GeoParish>()
                .WithMany()
                .HasForeignKey(a => a.ParishCode)
                .HasPrincipalKey(p => p.Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_bpl_parish");
        });

        builder.Property(e => e.Phone)
               .HasColumnName("phone")
               .HasMaxLength(20);

        builder.Property(e => e.Email)
               .HasColumnName("email")
               .HasMaxLength(254);

        builder.Property(e => e.IsPrimary)
               .HasColumnName("is_primary")
               .IsRequired()
               .HasDefaultValue(false);

        builder.Property(e => e.IsActive)
               .HasColumnName("is_active")
               .IsRequired()
               .HasDefaultValue(true);

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // ── FK a BusinessPartner ──────────────────────────────────────────────
        builder.HasOne<BusinessPartner>()
               .WithMany()
               .HasForeignKey(e => e.BusinessPartnerId)
               .OnDelete(DeleteBehavior.Restrict)
               .HasConstraintName("fk_bpl_business_partner");

        // ── Índices ───────────────────────────────────────────────────────────

        // Todas las ubicaciones de un BP
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId })
               .HasDatabaseName("ix_bpl_subscriber_bp");

        // Ubicaciones activas — query más frecuente
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId, e.IsActive })
               .HasDatabaseName("ix_bpl_subscriber_bp_active");

        // Única ubicación primaria activa por BP.
        // HasFilter genera la cláusula WHERE en la migración para PostgreSQL.
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId })
               .HasFilter("is_primary = true AND is_active = true")
               .IsUnique()
               .HasDatabaseName("uq_bpl_primary");
    }
}
