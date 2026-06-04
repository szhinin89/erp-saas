using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para CompanyBpTradingSettings.
///
/// CAMBIOS respecto a CompanyBusinessPartnerSettingsConfiguration:
///   - Tabla renombrada: master_company_bp_settings → master_company_bp_trading_settings
///   - Eliminado: price_list_id (FK a entidad inexistente)
///   - Agregado: blocked_reason, blocked_at, blocked_by (audit trail de bloqueo obligatorio)
///   - FK compuesto (business_partner_id, subscriber_id) → BP(id, subscriber_id). ADR-BP-13.
///
/// CONSTRAINT DE BLOQUEO:
///   La invariante "si IsBlocked=true entonces BlockedReason/At/By son obligatorios"
///   está en el dominio (CompanyBpTradingSettings.Block()) y en BD como CHECK constraint
///   creado via SQL raw en la migración:
///   ADD CONSTRAINT chk_cbts_block_consistency CHECK (
///     (is_blocked = false) OR
///     (is_blocked = true AND blocked_reason IS NOT NULL AND blocked_at IS NOT NULL AND blocked_by IS NOT NULL)
///   );
///
/// QUERY FILTER:
///   ICompanyScopedEntity + ISubscriberScopedEntity → strict company filter fail-closed.
///   (EnterpriseQueryFilterConfigurator.BuildStrictCompanyScopedFilter)
/// </summary>
public sealed class CompanyBpTradingSettingsConfiguration : IEntityTypeConfiguration<CompanyBpTradingSettings>
{
    public void Configure(EntityTypeBuilder<CompanyBpTradingSettings> builder)
    {
        builder.ToTable("master_company_bp_trading_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();

        builder.Property(x => x.CreditLimit)
               .HasColumnName("credit_limit")
               .HasPrecision(18, 2)
               .IsRequired()
               .HasDefaultValue(0m);

        builder.Property(x => x.CreditCurrencyCode)
               .HasColumnName("credit_currency_code")
               .HasMaxLength(CompanyBpTradingSettings.CurrencyCodeLen)
               .IsFixedLength()
               .IsRequired()
               .HasDefaultValue("USD");

        builder.Property(x => x.PaymentDays)
               .HasColumnName("payment_days")
               .IsRequired()
               .HasDefaultValue(0);

        builder.Property(x => x.IsBlocked)
               .HasColumnName("is_blocked")
               .IsRequired()
               .HasDefaultValue(false);

        // ── Campos de auditoría de bloqueo ───────────────────────────────────
        // Todos nullable — solo se populan cuando IsBlocked = true.
        // La consistencia (is_blocked=true → los tres obligatorios) está garantizada
        // por la invariante de dominio Block() y el CHECK constraint de BD.
        builder.Property(x => x.BlockedReason)
               .HasColumnName("blocked_reason")
               .HasMaxLength(CompanyBpTradingSettings.BlockedReasonMaxLen);

        builder.Property(x => x.BlockedAt)
               .HasColumnName("blocked_at");

        builder.Property(x => x.BlockedBy)
               .HasColumnName("blocked_by");

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── FK a BusinessPartner ──────────────────────────────────────────────
        builder.HasOne<BusinessPartner>()
               .WithMany()
               .HasForeignKey(x => x.BusinessPartnerId)
               .OnDelete(DeleteBehavior.Restrict)
               .HasConstraintName("fk_cbts_business_partner");

        // ── Índices ───────────────────────────────────────────────────────────

        // Una única configuración por empresa+BP (upsert semántico)
        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId, x.BusinessPartnerId })
               .IsUnique()
               .HasDatabaseName("uq_cbts_company_bp");

        // Todos los settings de una empresa — para listas de configuración
        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId })
               .HasDatabaseName("ix_cbts_subscriber_company");

        // Socios bloqueados en una empresa — query frecuente en validación documental
        // Permite verificar rápidamente si un BP está bloqueado antes de emitir un documento
        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId })
               .HasFilter("is_blocked = true")
               .HasDatabaseName("ix_cbts_blocked");
    }
}
