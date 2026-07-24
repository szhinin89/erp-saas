using ERP.Domain.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        builder.ToTable("price_list_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<PriceList>()
            .WithMany()
            .HasForeignKey(x => x.PriceListId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ItemId)
            .HasDatabaseName("ix_price_list_items_item");

        builder.HasIndex(x => new { x.PriceListId, x.ItemId })
            .IsUnique()
            .HasDatabaseName("uq_price_list_items_list_item");
    }
}
