using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para SupplierRetentionDefault —
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01.
///
/// FK a BusinessPartner y a SriRetentionCode son de columna simple. Cross-tenant safety
/// garantizada por el query filter global (fail-closed), no por FK compuesta.
///
/// QUERY FILTER:
///   ICompanyScopedEntity + ITenantScopedEntity → strict company filter fail-closed.
///   (EnterpriseQueryFilterConfigurator.BuildStrictCompanyScopedFilter)
/// </summary>
public sealed class SupplierRetentionDefaultConfiguration
    : IEntityTypeConfiguration<SupplierRetentionDefault>
{
    public void Configure(EntityTypeBuilder<SupplierRetentionDefault> builder)
    {
        builder.ToTable("master_supplier_retention_defaults");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.BusinessPartnerId)
            .HasColumnName("business_partner_id")
            .IsRequired();
        builder.Property(x => x.SriRetentionCodeId).HasColumnName("sri_retention_code_id").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired().HasDefaultValue(0);

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── FK a BusinessPartner ──────────────────────────────────────────────
        builder
            .HasOne<ERP.Domain.MasterData.Entities.BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_srd_business_partner");

        // ── FK a SriRetentionCode (SSOT dinámico — nunca string suelto) ────────
        builder
            .HasOne<SriRetentionCode>()
            .WithMany()
            .HasForeignKey(x => x.SriRetentionCodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_srd_sri_retention_code");

        // ── Índices ───────────────────────────────────────────────────────────

        // Un mismo código de retención no se repite para el mismo proveedor+empresa.
        // Sin unicidad por TaxType: un proveedor puede tener varios códigos activos del mismo
        // impuesto — esa es la razón funcional de esta feature.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BusinessPartnerId,
                x.SriRetentionCodeId,
            })
            .IsUnique()
            .HasDatabaseName("uq_srd_company_bp_code");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessPartnerId, x.IsActive })
            .HasDatabaseName("ix_srd_company_bp_active");
    }
}
