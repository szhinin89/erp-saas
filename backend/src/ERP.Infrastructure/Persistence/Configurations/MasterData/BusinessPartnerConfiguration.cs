using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuraci�n EF Core para BusinessPartner.
///
/// CAMBIOS respecto al modelo anterior:
///   - Eliminado: email, phone, legal_representative_name, navigations CustomerProfile/SupplierProfile
///   - Agregado: legal_entity_type_code, PersonName OwnsOne (legal_name + trade_name), CountryCode char(2)
///   - AlternateKey(Id, tenantId): permite FK compuesto desde roles/locations/contacts.
///     Garantiza cross-tenant safety a nivel BD. Ver ADR-BP-13 (Fase 4).
///
/// INDICE UNICO de identificacion (uq_mbp_identification):
///   tenant_id + identification_type + identification_number. Es INCONDICIONAL
///   (sin WHERE is_active) - ver ADR-BP-03 (Fase 3). Es la barrera real contra
///   duplicados; el pre-check ExistsByIdentificationAsync en los handlers es
///   solo optimizacion de UX para el caso comun.
///
/// QUERY FILTER:
///   Aplicado autom�ticamente por EnterpriseQueryFilterConfigurator
///   (ITenantScopedEntity ? subscriber fail-closed).
/// </summary>
public sealed class BusinessPartnerConfiguration : IEntityTypeConfiguration<BusinessPartner>
{
    public void Configure(EntityTypeBuilder<BusinessPartner> builder)
    {
        builder.ToTable("master_business_partners");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        // -- AlternateKey compuesto: permite que roles/locations/contacts referencien
        //    (business_partner_id, subscriber_id) ? (id, subscriber_id).
        //    Garantiza a nivel BD que no puede existir una FK cross-tenant. ADR-BP-13.
        builder.HasAlternateKey(x => new { x.Id, x.TenantId }).HasName("uq_mbp_id_subscriber");

        // -- TaxIdentification VO (owned, columnas aplanadas) -----------------
        builder.OwnsOne(
            x => x.Identification,
            id =>
            {
                id.Property(v => v.Type)
                    .HasColumnName("identification_type")
                    .HasMaxLength(TaxIdentification.TypeMaxLen)
                    .IsRequired();
                id.Property(v => v.Number)
                    .HasColumnName("identification_number")
                    .HasMaxLength(TaxIdentification.NumberMaxLen)
                    .IsRequired();
            }
        );

        // -- PersonName VO (owned, columnas aplanadas) ------------------------
        builder.OwnsOne(
            x => x.Name,
            pn =>
            {
                pn.Property(v => v.LegalName)
                    .HasColumnName("legal_name")
                    .HasMaxLength(PersonName.LegalNameMaxLen)
                    .IsRequired();
                pn.Property(v => v.TradeName)
                    .HasColumnName("trade_name")
                    .HasMaxLength(PersonName.TradeNameMaxLen);
            }
        );

        builder
            .Property(x => x.LegalEntityTypeCode)
            .HasColumnName("legal_entity_type_code")
            .IsRequired();
        builder
            .HasOne<LegalEntityTypeCatalog>()
            .WithMany()
            .HasForeignKey(x => x.LegalEntityTypeCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .Property(x => x.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(BusinessPartner.CountryCodeLen)
            .IsFixedLength();

        builder
            .Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);

        // -- Audit ------------------------------------------------------------
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // -- �ndices -----------------------------------------------------------
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_mbp_subscriber");

        builder
            .HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("ix_mbp_subscriber_active");

        // Indice unico incondicional (sin WHERE is_active) - ver ADR-BP-03 (Fase 3).
        // Creado via SQL raw en la migracion: EF Core no puede expresar un indice
        // compuesto sobre columnas del owner + un owned type (probado: tanto la
        // lambda x => new { x.TenantId, x.Identification.Type, ... } como el
        // overload de HasIndex(string[]) fallan en tiempo de diseno). Ver migracion
        // AddBusinessPartnerIdentificationUniqueIndex.
        //   CREATE UNIQUE INDEX uq_mbp_identification
        //   ON master_business_partners (tenant_id, identification_type, identification_number);
    }
}
