using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class CompanyBpSalesSettingsConfiguration
    : IEntityTypeConfiguration<CompanyBpSalesSettings>
{
    public void Configure(EntityTypeBuilder<CompanyBpSalesSettings> builder)
    {
        builder.ToTable("master_company_bp_sales_settings");

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
            .HasConstraintName("fk_cbss_business_partner");

        // ── Índices ───────────────────────────────────────────────────────────

        // Una única configuración por empresa+cliente (upsert semántico, Fase 3d)
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BusinessPartnerId,
            })
            .IsUnique()
            .HasDatabaseName("uq_cbss_company_bp");
    }
}
