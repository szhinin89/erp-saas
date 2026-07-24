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

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(PriceList.MaxCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(PriceList.MaxNameLength).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PriceList.CurrencyCodeLen).IsRequired();
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");

        builder.Property(x => x.RuleType).HasColumnName("rule_type").HasConversion<int?>();
        builder.Property(x => x.RuleValue).HasColumnName("rule_value").HasColumnType("numeric(18,6)");

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_price_lists_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_price_lists_tenant_company_code");
    }
}
