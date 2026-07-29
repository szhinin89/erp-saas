using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para BusinessPartnerRole (nuevo Aggregate Root).
///
/// DISEÑO DE TABLAS:
///   master_bp_roles           — rol base con audit + notas
///   master_bp_supplier_configs — config específica Supplier (OwnsOne split table, 1:1)
///   master_bp_carrier_configs  — config específica Carrier  (OwnsOne split table, 1:1)
///
///   Si el rol no tiene config del tipo, no existe fila en la tabla config. ADR-BP-11.
///
/// FK a BusinessPartner es de columna simple. Cross-tenant safety garantizada por el
/// query filter global (fail-closed), no por FK compuesta.
///
/// ÚNICA FK COMPUESTA en configs:
///   OwnsOne split table: EF Core usa el PK del owner (role_id) como PK+FK en la tabla config.
///   Subscriber_id no se denormaliza en configs — los configs siempre se cargan vía el rol,
///   que ya está filtrado por subscriber. No se necesita filtro independiente en configs.
///
/// QUERY FILTER:
///   Aplicado automáticamente (ITenantScopedEntity → subscriber fail-closed).
///
/// ÍNDICE ÚNICO DE ROL:
///   uq_bpr_bp_role: (subscriber_id, business_partner_id, role_type) UNCONDITIONAL.
///   Un rol de cada tipo por BP, activo o revocado. Ver ADR-BP-12 (UPSERT semántico).
/// </summary>
public sealed class BusinessPartnerRoleConfiguration : IEntityTypeConfiguration<BusinessPartnerRole>
{
    public void Configure(EntityTypeBuilder<BusinessPartnerRole> builder)
    {
        builder.ToTable("master_bp_roles");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(r => r.BusinessPartnerId)
            .HasColumnName("business_partner_id")
            .IsRequired();

        // AlternateKey para que las tablas de config puedan referenciar (id, subscriber_id)
        builder.HasAlternateKey(r => new { r.Id, r.TenantId }).HasName("uq_bpr_id_subscriber");

        builder
            .Property(r => r.RoleType)
            .HasColumnName("role_type")
            .HasConversion<short>()
            .IsRequired();

        builder
            .Property(r => r.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder
            .Property(r => r.Notes)
            .HasColumnName("notes")
            .HasMaxLength(BusinessPartnerRole.NotesMaxLen);

        builder.Property(r => r.AssignedAt).HasColumnName("assigned_at").IsRequired();
        builder.Property(r => r.AssignedBy).HasColumnName("assigned_by").IsRequired();
        builder.Property(r => r.RevokedAt).HasColumnName("revoked_at");
        builder.Property(r => r.RevokedBy).HasColumnName("revoked_by");

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");

        // ── SupplierRoleConfig — tabla separada (1:1, PK = FK al rol) ────────
        builder.OwnsOne(
            r => r.SupplierConfig,
            sc =>
            {
                sc.ToTable("master_bp_supplier_configs");

                sc.WithOwner().HasForeignKey("role_id").HasConstraintName("fk_bpsc_role");

                sc.Property(c => c.DefaultTaxSupportCode)
                    .HasColumnName("default_tax_support_code")
                    .HasMaxLength(SupplierRoleConfig.SriCodeMaxLen);

                sc.Property(c => c.DefaultRetentionVatCode)
                    .HasColumnName("default_retention_vat_code")
                    .HasMaxLength(SupplierRoleConfig.SriCodeMaxLen);

                sc.Property(c => c.DefaultRetentionIncomeCode)
                    .HasColumnName("default_retention_income_code")
                    .HasMaxLength(SupplierRoleConfig.SriCodeMaxLen);

                sc.Property(c => c.PaymentTerms)
                    .HasColumnName("payment_terms")
                    .HasMaxLength(SupplierRoleConfig.PaymentTermsMaxLen);

                // ── S3-A: nuevos campos SRI ──────────────────────────────────────
                sc.Property(c => c.DefaultPaymentMethodCode)
                    .HasColumnName("default_payment_method_code")
                    .HasMaxLength(SupplierRoleConfig.PaymentMethodCodeMaxLen);

                sc.Property(c => c.RefundProviderTypeCode)
                    .HasColumnName("refund_provider_type_code")
                    .HasMaxLength(SupplierRoleConfig.SriCodeMaxLen);

                sc.Property(c => c.IsRetentionExempt)
                    .HasColumnName("is_retention_exempt")
                    .IsRequired()
                    .HasDefaultValue(false);

                sc.Property(c => c.IsRequiredToKeepAccounting)
                    .HasColumnName("is_required_to_keep_accounting")
                    .IsRequired()
                    .HasDefaultValue(false);

                sc.Property(c => c.PaymentTermId).HasColumnName("payment_term_id").IsRequired();
            }
        );

        // ── SupplierClassificationConfig — tabla separada (1:1, PK = FK al rol) ─
        builder.OwnsOne(
            r => r.ClassificationConfig,
            scc =>
            {
                scc.ToTable("master_bp_supplier_classification_configs");

                scc.WithOwner().HasForeignKey("role_id").HasConstraintName("fk_bpscc_role");

                scc.Property(c => c.SupplierCategory)
                    .HasColumnName("supplier_category")
                    .HasMaxLength(SupplierClassificationConfig.CategoryMaxLen);

                scc.Property(c => c.SupplierType)
                    .HasColumnName("supplier_type")
                    .HasMaxLength(SupplierClassificationConfig.TypeMaxLen);

                scc.Property(c => c.SupplierRisk)
                    .HasColumnName("supplier_risk")
                    .HasMaxLength(SupplierClassificationConfig.RiskMaxLen);

                scc.Property(c => c.SupplierRating)
                    .HasColumnName("supplier_rating")
                    .HasMaxLength(SupplierClassificationConfig.RatingMaxLen);

                scc.Property(c => c.PrimaryGoodType)
                    .HasColumnName("primary_good_type")
                    .HasMaxLength(SupplierClassificationConfig.GoodTypeMaxLen);

                scc.Property(c => c.SupplierSegment)
                    .HasColumnName("supplier_segment")
                    .HasMaxLength(SupplierClassificationConfig.SegmentMaxLen);

                scc.Property(c => c.PaymentMethodPreference)
                    .HasColumnName("payment_method_preference")
                    .HasMaxLength(SupplierClassificationConfig.PaymentPrefMaxLen);
            }
        );

        // ── CarrierRoleConfig — tabla separada (1:1, PK = FK al rol) ─────────
        builder.OwnsOne(
            r => r.CarrierConfig,
            cc =>
            {
                cc.ToTable("master_bp_carrier_configs");

                cc.WithOwner().HasForeignKey("role_id").HasConstraintName("fk_bpcc_role");

                cc.Property(c => c.TransportAuthorizationNumber)
                    .HasColumnName("transport_authorization_number")
                    .HasMaxLength(CarrierRoleConfig.AuthNumberMaxLen);

                cc.Property(c => c.VehicleCapacityTons)
                    .HasColumnName("vehicle_capacity_tons")
                    .HasPrecision(10, 2);
            }
        );

        // ── CustomerRoleConfig — tabla separada (1:1, PK = FK al rol) ───────────
        builder.OwnsOne(
            r => r.CustomerConfig,
            crc =>
            {
                crc.ToTable("master_bp_customer_configs");

                crc.WithOwner().HasForeignKey("role_id").HasConstraintName("fk_bpcrc_role");

                crc.Property(c => c.CustomerCategory)
                    .HasColumnName("customer_category")
                    .HasMaxLength(CustomerRoleConfig.CategoryMaxLen);

                crc.Property(c => c.CustomerSegment)
                    .HasColumnName("customer_segment")
                    .HasMaxLength(CustomerRoleConfig.SegmentMaxLen);

                crc.Property(c => c.SalesZone)
                    .HasColumnName("sales_zone")
                    .HasMaxLength(CustomerRoleConfig.SalesZoneMaxLen);

                crc.Property(c => c.CreditRating)
                    .HasColumnName("credit_rating")
                    .HasMaxLength(CustomerRoleConfig.CreditRatingMaxLen);

                crc.Property(c => c.LoyaltyTier)
                    .HasColumnName("loyalty_tier")
                    .HasMaxLength(CustomerRoleConfig.LoyaltyTierMaxLen);

                crc.Property(c => c.PreferredInvoiceFormat)
                    .HasColumnName("preferred_invoice_format")
                    .HasMaxLength(CustomerRoleConfig.InvoiceFormatMaxLen);

                crc.Property(c => c.CustomerClassification)
                    .HasColumnName("customer_classification")
                    .HasMaxLength(CustomerRoleConfig.ClassificationMaxLen);

                // Índices para queries de CRM / segmentación por campo
                crc.HasIndex("role_id").HasDatabaseName("ix_bpcrc_role");
            }
        );

        // ── FK a BusinessPartner ──────────────────────────────────────────────
        // FK de columna simple. Cross-tenant safety garantizada por global query filter (fail-closed).
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(r => r.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bpr_business_partner");

        // ── Índices ───────────────────────────────────────────────────────────

        // Invariante de unicidad de rol — incondicional, activo o revocado. ADR-BP-12.
        builder
            .HasIndex(r => new
            {
                r.TenantId,
                r.BusinessPartnerId,
                r.RoleType,
            })
            .IsUnique()
            .HasDatabaseName("uq_bpr_bp_role");

        // Cargar todos los roles de un BP
        builder
            .HasIndex(r => new { r.TenantId, r.BusinessPartnerId })
            .HasDatabaseName("ix_bpr_subscriber_bp");

        // Encontrar todos los Customers/Suppliers/Carriers del tenant
        builder
            .HasIndex(r => new { r.TenantId, r.RoleType })
            .HasDatabaseName("ix_bpr_subscriber_type");

        // Roles activos por tipo — query más frecuente en documentos
        builder
            .HasIndex(r => new
            {
                r.TenantId,
                r.RoleType,
                r.IsActive,
            })
            .HasDatabaseName("ix_bpr_subscriber_type_active");
    }
}
