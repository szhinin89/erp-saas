using System.Text.Json;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Needed for nameof() FK expressions below

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");

        // ── PK + Tenant ───────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        // ── ItemType enum → string ────────────────────────────────────────
        builder.Property(x => x.ItemType)
            .HasColumnName("item_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observations).HasColumnName("observations").HasColumnType("text");

        // ── Classification ────────────────────────────────────────────────
        builder.Property(x => x.CategoryNodeId).HasColumnName("category_node_id");
        builder.Property(x => x.BrandId).HasColumnName("brand_id");
        builder.Property(x => x.DefaultUomCode).HasColumnName("default_uom_code").HasMaxLength(10).IsRequired();

        // ── ItemCode VO (OwnsOne — flattened) ─────────────────────────────
        builder.OwnsOne(x => x.Code, code =>
        {
            code.Property(c => c.SKU).HasColumnName("sku").HasMaxLength(50).IsRequired();
            code.Property(c => c.ShortName).HasColumnName("short_name").HasMaxLength(50).IsRequired();
            code.Property(c => c.Description).HasColumnName("description").HasMaxLength(254).IsRequired();
            code.Property(c => c.PurchaseCode).HasColumnName("purchase_code").HasMaxLength(50);
        });

        // ── ItemTaxConfig VO ──────────────────────────────────────────────
        builder.OwnsOne(x => x.TaxConfig, tax =>
        {
            tax.Property(t => t.AppliesVatOnSale).HasColumnName("applies_vat_on_sale").IsRequired();
            tax.Property(t => t.AppliesVatOnPurchase).HasColumnName("applies_vat_on_purchase").IsRequired();
            tax.Property(t => t.AppliesExciseTax).HasColumnName("applies_excise_tax").IsRequired();
            tax.Property(t => t.SaleVatCode).HasColumnName("sale_vat_code").HasMaxLength(10);
            tax.Property(t => t.PurchaseVatCode).HasColumnName("purchase_vat_code").HasMaxLength(10);
            tax.Property(t => t.ExciseTaxCode).HasColumnName("excise_tax_code").HasMaxLength(10);
            tax.Property(t => t.VatAccountId).HasColumnName("vat_account_id");
            tax.Property(t => t.PurchaseVatAccountId).HasColumnName("purchase_vat_account_id");
            tax.Property(t => t.ExciseAccountId).HasColumnName("excise_account_id");
            tax.Property(t => t.SriServiceCode).HasColumnName("sri_service_code").HasMaxLength(5);
        });

        // ── ItemSaleConfig VO ─────────────────────────────────────────────
        builder.OwnsOne(x => x.SaleConfig, sale =>
        {
            sale.Property(s => s.IsForSale).HasColumnName("is_for_sale").IsRequired();
            sale.Property(s => s.MaxDiscountPercent).HasColumnName("max_discount_percent").HasPrecision(5, 2);
            sale.Property(s => s.IsAvailableOnWeb).HasColumnName("available_on_web").IsRequired();
            sale.Property(s => s.IsAvailableOnPOS).HasColumnName("available_on_pos").IsRequired();
            sale.Property(s => s.IsAvailableOnMobile).HasColumnName("available_on_mobile").IsRequired();
            sale.Property(s => s.IsEcommerceActive).HasColumnName("is_ecommerce_active").IsRequired();
            sale.Property(s => s.IsFavorite).HasColumnName("is_favorite").IsRequired();
        });

        // ── ItemStockConfig VO ────────────────────────────────────────────
        builder.OwnsOne(x => x.StockConfig, stock =>
        {
            stock.Property(s => s.TracksStock).HasColumnName("tracks_stock").IsRequired();
            stock.Property(s => s.TracksLot).HasColumnName("tracks_lot").IsRequired();
            stock.Property(s => s.TracksSeries).HasColumnName("tracks_series").IsRequired();
            stock.Property(s => s.AllowDecimalQty).HasColumnName("allow_decimal_qty").IsRequired();
            stock.Property(s => s.AllowDecimalSale).HasColumnName("allow_decimal_sale").IsRequired();
            stock.Property(s => s.MinStockQty).HasColumnName("min_stock_qty").HasPrecision(14, 4);
            stock.Property(s => s.MaxStockQty).HasColumnName("max_stock_qty").HasPrecision(14, 4);
        });

        // ── JSONB Flexible Attributes ─────────────────────────────────────
        builder.Property(x => x.Specifications)
            .HasColumnName("specifications")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, _json),
                v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, _json) ?? new())
            .IsRequired();

        builder.Property(x => x.MarketingAttributes)
            .HasColumnName("marketing_attributes")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, _json),
                v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, _json) ?? new())
            .IsRequired();

        // ── MasterEntity fields ────────────────────────────────────────────
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Collections (explicit FK — avoids shadow property concurrency issues) ──
        builder.HasMany(x => x.Variants)
            .WithOne()
            .HasForeignKey(nameof(ItemVariant.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(nameof(ItemImage.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.UnitConversions)
            .WithOne()
            .HasForeignKey(nameof(ItemUnitConversion.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Substitutes)
            .WithOne()
            .HasForeignKey(nameof(ItemSubstitute.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.PackagingLevels)
            .WithOne()
            .HasForeignKey(nameof(ItemPackagingLevel.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.SubscriberId)
            .HasDatabaseName("ix_items_subscriber");

        builder.HasIndex(x => new { x.SubscriberId, x.ItemType })
            .HasDatabaseName("ix_items_subscriber_type");

        builder.HasIndex(x => new { x.SubscriberId, x.CategoryNodeId })
            .HasDatabaseName("ix_items_subscriber_category");

        // SKU unique per subscriber — enforced at DB level via migration annotation
        // HasIndex on OwnsOne shadow props requires raw SQL; defined in migration script
    }
}
