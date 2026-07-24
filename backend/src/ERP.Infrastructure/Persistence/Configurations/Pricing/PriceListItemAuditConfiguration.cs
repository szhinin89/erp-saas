using ERP.Domain.Modules.Pricing.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceListItemAuditConfiguration : IEntityTypeConfiguration<PriceListItemAudit>
{
    public void Configure(EntityTypeBuilder<PriceListItemAudit> builder)
    {
        builder.ConfigureAuditBase("price_list_item_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ItemId, x.OccurredAtUtc })
            .HasDatabaseName("ix_price_list_item_audit_item_occurred_at");
        builder.HasIndex(x => new { x.TenantId, x.PriceListId, x.OccurredAtUtc })
            .HasDatabaseName("ix_price_list_item_audit_price_list_occurred_at");
    }
}
