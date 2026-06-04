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
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
        builder.Property(x => x.PriceIncludesVat).HasColumnName("price_includes_vat").IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from").HasColumnType("date").IsRequired();
        builder.Property(x => x.ValidTo).HasColumnName("valid_to").HasColumnType("date");
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.Target)
            .HasColumnName("target")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Entries)
            .WithOne()
            .HasForeignKey(nameof(PriceListEntry.PriceListId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Discounts)
            .WithOne()
            .HasForeignKey(nameof(PriceListDiscount.PriceListId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_price_lists_subscriber_company_code");

        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId, x.IsDefault })
            .HasDatabaseName("ix_price_lists_default");
    }
}

public sealed class PriceListEntryConfiguration : IEntityTypeConfiguration<PriceListEntry>
{
    public void Configure(EntityTypeBuilder<PriceListEntry> builder)
    {
        builder.ToTable("price_list_entries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.UomCode).HasColumnName("uom_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Price).HasColumnName("price").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.MinQty).HasColumnName("min_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => x.ItemId).HasDatabaseName("ix_price_list_entries_item");
        builder.HasIndex(x => new { x.PriceListId, x.ItemId })
            .HasDatabaseName("ix_price_list_entries_list_item");
    }
}

public sealed class PriceListDiscountConfiguration : IEntityTypeConfiguration<PriceListDiscount>
{
    public void Configure(EntityTypeBuilder<PriceListDiscount> builder)
    {
        builder.ToTable("price_list_discounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.CategoryNodeId).HasColumnName("category_node_id");
        builder.Property(x => x.DiscountType)
            .HasColumnName("discount_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnName("discount_value").HasPrecision(10, 4).IsRequired();
        builder.Property(x => x.MinQty).HasColumnName("min_qty").HasPrecision(14, 4);
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from").HasColumnType("date");
        builder.Property(x => x.ValidTo).HasColumnName("valid_to").HasColumnType("date");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}
