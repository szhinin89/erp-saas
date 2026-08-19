using ERP.Domain.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(PriceList.MaxCodeLength)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(PriceList.MaxNameLength)
            .IsRequired();
        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(PriceList.CurrencyCodeLen)
            .IsRequired();
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");

        builder.Property(x => x.RuleType).HasColumnName("rule_type").HasConversion<int?>();
        builder
            .Property(x => x.RuleValue)
            .HasColumnName("rule_value")
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_price_lists_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.Code,
            })
            .IsUnique()
            .HasDatabaseName("uq_price_lists_tenant_company_code");

        // CONFIG-FOUNDATION-P0-01: garantiza en DB que solo exista una lista de precios
        // default por empresa (Fase 10 de docs/architecture/configuration-engine-target-architecture.md).
        // Incluye IsDefault en la tupla de columnas (aunque el filtro ya la fija en true) porque
        // EF Core identifica un índice por su lista exacta de propiedades: usar la misma tupla
        // (TenantId, CompanyId) que "ix_price_lists_tenant_company" hace que el segundo
        // HasIndex() reconfigure/renombre el primero en vez de crear un índice nuevo,
        // eliminando en silencio el índice general de lookup — confirmado al inspeccionar la
        // migración generada antes de aplicarla.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.IsDefault,
            })
            .IsUnique()
            .HasDatabaseName("uq_price_lists_tenant_company_default")
            .HasFilter("is_default = true");
    }
}
