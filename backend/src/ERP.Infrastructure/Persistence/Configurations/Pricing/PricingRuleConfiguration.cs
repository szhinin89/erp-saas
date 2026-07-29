using ERP.Domain.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("pricing_rules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder
            .Property(x => x.RuleType)
            .HasColumnName("rule_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.RuleValue)
            .HasColumnName("rule_value")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasOne<PriceList>()
            .WithMany()
            .HasForeignKey(x => x.PriceListId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ItemId).HasDatabaseName("ix_pricing_rules_item");

        builder
            .HasIndex(x => new { x.PriceListId, x.ItemId })
            .IsUnique()
            .HasDatabaseName("uq_pricing_rules_list_item");
    }
}
