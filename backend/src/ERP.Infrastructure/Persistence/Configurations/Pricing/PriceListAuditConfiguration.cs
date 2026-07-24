using ERP.Domain.Modules.Pricing.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceListAuditConfiguration : IEntityTypeConfiguration<PriceListAudit>
{
    public void Configure(EntityTypeBuilder<PriceListAudit> builder)
    {
        builder.ConfigureAuditBase("price_list_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.OldRuleType).HasColumnName("old_rule_type").HasConversion<int?>();
        builder.Property(x => x.OldRuleValue).HasColumnName("old_rule_value").HasColumnType("numeric(18,6)");
        builder.Property(x => x.NewRuleType).HasColumnName("new_rule_type").HasConversion<int?>();
        builder.Property(x => x.NewRuleValue).HasColumnName("new_rule_value").HasColumnType("numeric(18,6)");
    }
}
