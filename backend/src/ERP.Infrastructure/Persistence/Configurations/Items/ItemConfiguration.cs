using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Needed for nameof() FK expressions below

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");

        // ── PK + Tenant ───────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        // ── ItemTypeId — FK a catálogo tenant-editable (item_types.id) ─
        builder.Property(x => x.ItemTypeId)
            .HasColumnName("item_type_id")
            .IsRequired();

        builder.Property(x => x.Observations).HasColumnName("observations").HasColumnType("text");

        // ── Classification ────────────────────────────────────────────────
        builder.Property(x => x.CategoryNodeId).HasColumnName("category_node_id");
        builder.Property(x => x.BrandId).HasColumnName("brand_id");
        builder.Property(x => x.DefaultUomCode).HasColumnName("default_uom_code").HasMaxLength(10).IsRequired();

        // ── Precio base (SSOT, Motor de Pricing v2) ───────────────────────
        builder.Property(x => x.BaseSalePrice).HasColumnName("base_sale_price").HasColumnType("numeric(18,6)");

        // ── ItemCode VO (OwnsOne — flattened) ─────────────────────────────
        builder.OwnsOne(x => x.Code, code =>
        {
            code.Property(c => c.SKU).HasColumnName("sku").HasMaxLength(50).IsRequired();
            code.Property(c => c.ShortName).HasColumnName("short_name").HasMaxLength(50).IsRequired();
            code.Property(c => c.Description).HasColumnName("description").HasMaxLength(254).IsRequired();
        });

        // ── ItemTaxConfig VO ──────────────────────────────────────────────
        builder.OwnsOne(x => x.TaxConfig, tax =>
        {
            tax.Property(t => t.SaleVatCode).HasColumnName("sale_vat_code").HasMaxLength(10);
            tax.Property(t => t.PurchaseVatCode).HasColumnName("purchase_vat_code").HasMaxLength(10);
            tax.Property(t => t.ExciseTaxCode).HasColumnName("excise_tax_code").HasMaxLength(10);
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

        // ── MasterEntity fields ────────────────────────────────────────────
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);
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

        builder.HasMany(x => x.SupplierCodes)
            .WithOne()
            .HasForeignKey(nameof(ItemSupplierCode.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_items_subscriber");

        builder.HasIndex(x => new { x.TenantId, x.ItemTypeId })
            .HasDatabaseName("ix_items_subscriber_type");

        builder.HasIndex(x => new { x.TenantId, x.CategoryNodeId })
            .HasDatabaseName("ix_items_subscriber_category");

        // SKU es clave de negocio única por tenant (editable — ver Item.UpdateSku).
        // NOTA (verificado 2026-07-07, auditoría de cierre): se intentó declarar el índice
        // único compuesto (tenant_id, sku) vía Fluent API — tanto `x => new { x.TenantId, x.Code.SKU }`
        // como el overload de string `HasIndex("TenantId", "Code.SKU")` — y ambos fallan en
        // tiempo de diseño con InvalidOperationException: EF Core no puede resolver una
        // propiedad de un tipo OwnsOne (Code.SKU) combinada con una propiedad del propio
        // owner (TenantId) en un mismo índice cuando el owned type comparte tabla con el
        // owner. Esto NO es una limitación de este código — es una limitación conocida de
        // EF Core con owned types de la misma tabla. La única forma real de resolverlo sería
        // promover SKU a propiedad directa de Item (fuera de ItemCode VO), lo cual es un
        // cambio de modelo de dominio, no de configuración EF — fuera de alcance de este
        // cierre. El índice se sigue creando por SQL crudo en la migración
        // Fase1ItemIdentityHardening; EF no lo conoce (el snapshot del modelo se deriva de
        // OnModelCreating, no del historial de migraciones), así que una regeneración futura
        // de migraciones seguiría sin reflejarlo — riesgo real y documentado, no resuelto.

        // ── Integridad referencial de clasificación ─────────────────────────
        builder.HasOne<ItemCategoryNode>()
            .WithMany()
            .HasForeignKey(x => x.CategoryNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── FK física a item_types(id) ────────────────────────────────────────
        builder.HasOne<ItemTypeDefinition>()
            .WithMany()
            .HasForeignKey(x => x.ItemTypeId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
