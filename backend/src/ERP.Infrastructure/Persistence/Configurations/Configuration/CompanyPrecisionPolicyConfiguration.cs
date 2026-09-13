using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Configuration;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Ver <see cref="CompanyPrecisionPolicy"/> para el contrato de
/// negocio completo.
/// </summary>
public sealed class CompanyPrecisionPolicyConfiguration : IEntityTypeConfiguration<CompanyPrecisionPolicy>
{
    public void Configure(EntityTypeBuilder<CompanyPrecisionPolicy> builder)
    {
        builder.ToTable(
            "company_precision_policy",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_company_precision_policy_sales_unit_price",
                    "sales_unit_price_decimals BETWEEN 2 AND 8"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_purchase_unit_price",
                    "purchase_unit_price_decimals BETWEEN 2 AND 8"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_quantity",
                    "quantity_decimals BETWEEN 0 AND 6"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_percentage",
                    "percentage_decimals BETWEEN 2 AND 6"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_unit_cost",
                    "unit_cost_decimals BETWEEN 2 AND 8"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_average_cost",
                    "average_cost_decimals BETWEEN 2 AND 8"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_conversion_factor",
                    "conversion_factor_decimals BETWEEN 2 AND 8"
                );
                t.HasCheckConstraint(
                    "ck_company_precision_policy_settlement_tolerance",
                    "settlement_tolerance_amount BETWEEN 0.00 AND 0.02"
                );
            }
        );

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(e => e.ProfileType)
            .HasColumnName("profile_type")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<PrecisionProfileType>(v, ignoreCase: true)
            )
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.SalesUnitPriceDecimals).HasColumnName("sales_unit_price_decimals").IsRequired();
        builder.Property(e => e.PurchaseUnitPriceDecimals).HasColumnName("purchase_unit_price_decimals").IsRequired();
        builder.Property(e => e.QuantityDecimals).HasColumnName("quantity_decimals").IsRequired();
        builder.Property(e => e.PercentageDecimals).HasColumnName("percentage_decimals").IsRequired();
        builder.Property(e => e.UnitCostDecimals).HasColumnName("unit_cost_decimals").IsRequired();
        builder.Property(e => e.AverageCostDecimals).HasColumnName("average_cost_decimals").IsRequired();
        builder.Property(e => e.ConversionFactorDecimals).HasColumnName("conversion_factor_decimals").IsRequired();

        builder
            .Property(e => e.SettlementToleranceAmount)
            .HasColumnName("settlement_tolerance_amount")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(e => e.IsLocked).HasColumnName("is_locked").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.LockedAt).HasColumnName("locked_at");
        builder.Property(e => e.LockedReason).HasColumnName("locked_reason");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(e => new { e.TenantId, e.CompanyId })
            .IsUnique()
            .HasDatabaseName("uq_company_precision_policy_tenant_company");

        builder
            .HasOne<ERP.Domain.Modules.Company.Entities.Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
