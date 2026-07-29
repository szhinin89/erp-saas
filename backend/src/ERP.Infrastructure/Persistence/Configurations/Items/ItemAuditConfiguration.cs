using ERP.Domain.Modules.Items.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemAuditConfiguration : IEntityTypeConfiguration<ItemAudit>
{
    public void Configure(EntityTypeBuilder<ItemAudit> builder)
    {
        builder.ConfigureAuditBase("item_audit");

        builder
            .Property(x => x.OldBaseSalePrice)
            .HasColumnName("old_base_sale_price")
            .HasColumnType("numeric(18,6)");
        builder
            .Property(x => x.NewBaseSalePrice)
            .HasColumnName("new_base_sale_price")
            .HasColumnType("numeric(18,6)");
    }
}
