using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para BusinessPartner.
///
/// CAMBIOS respecto al modelo anterior:
///   - Eliminado: email, phone, legal_representative_name, navigations CustomerProfile/SupplierProfile
///   - Agregado: person_type, PersonName OwnsOne (legal_name + trade_name), CountryCode char(2)
///   - AlternateKey(Id, SubscriberId): permite FK compuesto desde roles/locations/contacts.
///     Garantiza cross-tenant safety a nivel BD. Ver ADR-BP-13 (Fase 4).
///
/// ÍNDICE ÚNICO de identificación:
///   Creado via SQL raw en la migración (limitación EF Core con owned types en índice compuesto).
///   Es INCONDICIONAL (sin WHERE is_active) — ver ADR-BP-03 (Fase 3).
///
/// QUERY FILTER:
///   Aplicado automáticamente por EnterpriseQueryFilterConfigurator
///   (ISubscriberScopedEntity → subscriber fail-closed).
/// </summary>
public sealed class BusinessPartnerConfiguration : IEntityTypeConfiguration<BusinessPartner>
{
    public void Configure(EntityTypeBuilder<BusinessPartner> builder)
    {
        builder.ToTable("master_business_partners");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        // ── AlternateKey compuesto: permite que roles/locations/contacts referencien
        //    (business_partner_id, subscriber_id) → (id, subscriber_id).
        //    Garantiza a nivel BD que no puede existir una FK cross-tenant. ADR-BP-13.
        builder.HasAlternateKey(x => new { x.Id, x.SubscriberId })
               .HasName("uq_mbp_id_subscriber");

        // ── TaxIdentification VO (owned, columnas aplanadas) ─────────────────
        builder.OwnsOne(x => x.Identification, id =>
        {
            id.Property(v => v.Type)
              .HasColumnName("identification_type")
              .HasMaxLength(TaxIdentification.TypeMaxLen)
              .IsRequired();
            id.Property(v => v.Number)
              .HasColumnName("identification_number")
              .HasMaxLength(TaxIdentification.NumberMaxLen)
              .IsRequired();
        });

        // ── PersonName VO (owned, columnas aplanadas) ────────────────────────
        builder.OwnsOne(x => x.Name, pn =>
        {
            pn.Property(v => v.LegalName)
              .HasColumnName("legal_name")
              .HasMaxLength(PersonName.LegalNameMaxLen)
              .IsRequired();
            pn.Property(v => v.TradeName)
              .HasColumnName("trade_name")
              .HasMaxLength(PersonName.TradeNameMaxLen);
        });

        builder.Property(x => x.PersonType)
               .HasColumnName("person_type")
               .HasConversion<short>()
               .IsRequired();

        builder.Property(x => x.CountryCode)
               .HasColumnName("country_code")
               .HasMaxLength(BusinessPartner.CountryCodeLen)
               .IsFixedLength();

        builder.Property(x => x.IsActive)
               .HasColumnName("is_active")
               .IsRequired()
               .HasDefaultValue(true);

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Índices ───────────────────────────────────────────────────────────
        builder.HasIndex(x => x.SubscriberId)
               .HasDatabaseName("ix_mbp_subscriber");

        builder.HasIndex(x => new { x.SubscriberId, x.IsActive })
               .HasDatabaseName("ix_mbp_subscriber_active");

        // Índice único incondicional — ver note en comentario de clase.
        // Migración lo crea con SQL raw:
        //   CREATE UNIQUE INDEX uq_mbp_identification
        //   ON master_business_partners (subscriber_id, identification_type, identification_number);
        // EF Core no puede expresar un índice compuesto owner+owned directamente.
    }
}
