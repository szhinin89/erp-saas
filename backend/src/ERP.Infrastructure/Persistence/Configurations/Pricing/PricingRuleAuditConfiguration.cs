using ERP.Domain.Modules.Pricing.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PricingRuleAuditConfiguration : IEntityTypeConfiguration<PricingRuleAudit>
{
    public void Configure(EntityTypeBuilder<PricingRuleAudit> builder)
    {
        builder.ConfigureAuditBase("pricing_rule_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder
            .Property(x => x.OldRuleType)
            .HasColumnName("old_rule_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.OldRuleValue)
            .HasColumnName("old_rule_value")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.NewRuleType)
            .HasColumnName("new_rule_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.NewRuleValue)
            .HasColumnName("new_rule_value")
            .HasColumnType("numeric(18,6)")
            .IsRequired();

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.ItemId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_pricing_rule_audit_item_occurred_at");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.PriceListId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_pricing_rule_audit_price_list_occurred_at");
    }
}
