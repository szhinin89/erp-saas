using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para BusinessPartnerContact.
///
/// CAMBIOS respecto al modelo anterior:
///   - Eliminado: company_id (scope cambiado a subscriber-only). ADR-BP-02 (Fase 4).
///   - Agregado: ContactInfo OwnsOne (phone, mobile, email aplanados).
///   - Agregado: other_description (obligatorio cuando ContactRole = Other).
///   - FK location_id: cambiado de SetNull a RESTRICT. Ver Problema 7 (Fase 4).
///     Justificación: el sistema usa soft delete — la FK nunca se activa en operación normal.
///     RESTRICT es la barrera administrativa contra DELETE físico accidental en BD.
///   - FK compuesto (business_partner_id, subscriber_id) → BP(id, subscriber_id). ADR-BP-13.
///
/// ÍNDICE PARCIAL ÚNICO is_primary:
///   Solo un contacto primario activo por (subscriber_id, business_partner_id).
///
/// QUERY FILTER:
///   ISubscriberScopedEntity → subscriber fail-closed (EnterpriseQueryFilterConfigurator).
/// </summary>
public sealed class BusinessPartnerContactConfiguration : IEntityTypeConfiguration<BusinessPartnerContact>
{
    public void Configure(EntityTypeBuilder<BusinessPartnerContact> builder)
    {
        builder.ToTable("master_bp_contacts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.LocationId).HasColumnName("location_id");

        builder.Property(e => e.FirstName)
               .HasColumnName("first_name")
               .HasMaxLength(BusinessPartnerContact.FirstNameMaxLen)
               .IsRequired();

        builder.Property(e => e.LastName)
               .HasColumnName("last_name")
               .HasMaxLength(BusinessPartnerContact.LastNameMaxLen);

        builder.Property(e => e.Position)
               .HasColumnName("position")
               .HasMaxLength(BusinessPartnerContact.PositionMaxLen);

        builder.Property(e => e.Role)
               .HasColumnName("contact_role")
               .HasConversion<short>()
               .IsRequired();

        builder.Property(e => e.OtherDescription)
               .HasColumnName("other_description")
               .HasMaxLength(BusinessPartnerContact.OtherDescriptionMaxLen);

        builder.Property(e => e.Notes)
               .HasColumnName("notes")
               .HasMaxLength(BusinessPartnerContact.NotesMaxLen);

        builder.Property(e => e.IsPrimary)
               .HasColumnName("is_primary")
               .IsRequired()
               .HasDefaultValue(false);

        builder.Property(e => e.IsActive)
               .HasColumnName("is_active")
               .IsRequired()
               .HasDefaultValue(true);

        // FullName es propiedad computada — no persiste en BD
        builder.Ignore(e => e.FullName);

        // ── ContactInfo VO (owned, columnas aplanadas en master_bp_contacts) ──
        builder.OwnsOne(e => e.Contact, ci =>
        {
            ci.Property(c => c.Phone)
              .HasColumnName("phone")
              .HasMaxLength(ContactInfo.PhoneMaxLen);

            ci.Property(c => c.Mobile)
              .HasColumnName("mobile")
              .HasMaxLength(ContactInfo.PhoneMaxLen);

            ci.Property(c => c.Email)
              .HasColumnName("email")
              .HasMaxLength(ContactInfo.EmailMaxLen);
        });

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
               .HasConstraintName("fk_bpc_business_partner");

        // ── FK a BusinessPartnerLocation (RESTRICT, no SetNull) ──────────────
        // SetNull fue eliminado. Ver Problema 7 (Fase 4).
        // El sistema usa soft delete: la FK nunca se activa en operación normal.
        // RESTRICT = barrera administrativa contra DELETE físico accidental.
        // El handler de DeactivateLocation debe verificar HasActiveContactsAsync() antes de proceder.
        builder.HasOne<BusinessPartnerLocation>()
               .WithMany()
               .HasForeignKey(e => e.LocationId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict)
               .HasConstraintName("fk_bpc_location");

        // ── Índices ───────────────────────────────────────────────────────────

        // Todos los contactos de un BP
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId })
               .HasDatabaseName("ix_bpc_subscriber_bp");

        // Contactos activos — query más frecuente
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId, e.IsActive })
               .HasDatabaseName("ix_bpc_subscriber_bp_active");

        // Buscar el contacto Legal o de Facturación de un BP específico
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId, e.Role })
               .HasDatabaseName("ix_bpc_subscriber_bp_role");

        // Qué contactos referencian una ubicación (para validar RESTRICT antes de desactivarla)
        builder.HasIndex(e => e.LocationId)
               .HasFilter("location_id IS NOT NULL")
               .HasDatabaseName("ix_bpc_location");

        // Único contacto primario activo por BP
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId })
               .HasFilter("is_primary = true AND is_active = true")
               .IsUnique()
               .HasDatabaseName("uq_bpc_primary");
    }
}
