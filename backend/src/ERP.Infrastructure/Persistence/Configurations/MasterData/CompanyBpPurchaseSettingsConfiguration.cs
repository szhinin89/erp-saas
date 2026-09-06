using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para CompanyBpPurchaseSettings — ADR-033, Fase 3.
///
/// Espejo de CompanyBpTradingSettingsConfiguration en scope y patrón, pero para el default de
/// proveedor (compras/gastos) en vez del default de cliente (ventas/crédito comercial) — entidades
/// separadas a propósito, no se fusionan.
///
/// FK a BusinessPartner es de columna simple. Cross-tenant safety garantizada por el
/// query filter global (fail-closed), no por FK compuesta.
///
/// QUERY FILTER:
///   ICompanyScopedEntity + ITenantScopedEntity → strict company filter fail-closed.
///   (EnterpriseQueryFilterConfigurator.BuildStrictCompanyScopedFilter)
/// </summary>
public sealed class CompanyBpPurchaseSettingsConfiguration
    : IEntityTypeConfiguration<CompanyBpPurchaseSettings>
{
    public void Configure(EntityTypeBuilder<CompanyBpPurchaseSettings> builder)
    {
        builder.ToTable("master_company_bp_purchase_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.BusinessPartnerId)
            .HasColumnName("business_partner_id")
            .IsRequired();

        builder.Property(x => x.PaymentTermId).HasColumnName("payment_term_id");

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── FK a BusinessPartner ──────────────────────────────────────────────
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cbps_business_partner");

        // ── Índices ───────────────────────────────────────────────────────────

        // Una única configuración por empresa+proveedor (upsert semántico, Fase 3d)
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BusinessPartnerId,
            })
            .IsUnique()
            .HasDatabaseName("uq_cbps_company_bp");
    }
}
