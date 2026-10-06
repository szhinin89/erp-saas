using ERP.Domain.Modules.Items.Entities;
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

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Nature).HasColumnName("nature").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(x => x.ParticipatesInInventory);
        builder.HasAlternateKey(x => new { x.Id, x.TenantId, x.CompanyId });
        builder.HasOne<ERP.Domain.Modules.Company.Entities.Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        // ── ItemTypeId — FK a catálogo tenant-editable (item_types.id) ─
        builder.Property(x => x.ItemTypeId).HasColumnName("item_type_id").IsRequired();

        builder.Property(x => x.Observations).HasColumnName("observations").HasColumnType("text");

        // ── Classification ────────────────────────────────────────────────
        builder.Property(x => x.CategoryNodeId).HasColumnName("category_node_id");
        builder.Property(x => x.BrandId).HasColumnName("brand_id");
        builder
            .Property(x => x.DefaultUomCode)
            .HasColumnName("default_uom_code")
            .HasMaxLength(10)
            .IsRequired();

        // ── Precio base (SSOT, Motor de Pricing v2) ───────────────────────
        builder
            .Property(x => x.BaseSalePrice)
            .HasColumnName("base_sale_price")
            .HasColumnType("numeric(18,6)");

        // ── ItemCode VO (OwnsOne — flattened) ─────────────────────────────
        builder.OwnsOne(
            x => x.Code,
            code =>
            {
                code.Property(c => c.SKU).HasColumnName("sku").HasMaxLength(50).IsRequired();
                code.Property(c => c.ShortName)
                    .HasColumnName("short_name")
                    .HasMaxLength(50)
                    .IsRequired();
                code.Property(c => c.Description)
                    .HasColumnName("description")
                    .HasMaxLength(254)
                    .IsRequired();
            }
        );

        // ── ItemTaxConfig VO ──────────────────────────────────────────────
        builder.OwnsOne(
            x => x.TaxConfig,
            tax =>
            {
                tax.Property(t => t.SaleVatCode).HasColumnName("sale_vat_code").HasMaxLength(10);
                tax.Property(t => t.PurchaseVatCode)
                    .HasColumnName("purchase_vat_code")
                    .HasMaxLength(10);
                tax.Property(t => t.ExciseTaxCode)
                    .HasColumnName("excise_tax_code")
                    .HasMaxLength(10);
            }
        );

        // ── ItemSaleConfig VO ─────────────────────────────────────────────
        builder.OwnsOne(
            x => x.SaleConfig,
            sale =>
            {
                sale.Property(s => s.IsForSale).HasColumnName("is_for_sale").IsRequired();
                sale.Property(s => s.MaxDiscountPercent)
                    .HasColumnName("max_discount_percent")
                    .HasColumnType("numeric(9,6)");
                sale.Property(s => s.IsAvailableOnWeb)
                    .HasColumnName("available_on_web")
                    .IsRequired();
                sale.Property(s => s.IsAvailableOnPOS)
                    .HasColumnName("available_on_pos")
                    .IsRequired();
                sale.Property(s => s.IsAvailableOnMobile)
                    .HasColumnName("available_on_mobile")
                    .IsRequired();
                sale.Property(s => s.IsEcommerceActive)
                    .HasColumnName("is_ecommerce_active")
                    .IsRequired();
                sale.Property(s => s.IsFavorite).HasColumnName("is_favorite").IsRequired();
            }
        );

        // ── ItemStockConfig VO ────────────────────────────────────────────
        builder.OwnsOne(
            x => x.StockConfig,
            stock =>
            {
                stock.Property(s => s.StockControlEnabled).HasColumnName("stock_control_enabled").IsRequired();
                stock.Property(s => s.TracksLot).HasColumnName("tracks_lot").IsRequired();
                stock.Property(s => s.TracksSeries).HasColumnName("tracks_series").IsRequired();
                stock
                    .Property(s => s.AllowDecimalQty)
                    .HasColumnName("allow_decimal_qty")
                    .IsRequired();
                stock
                    .Property(s => s.AllowDecimalSale)
                    .HasColumnName("allow_decimal_sale")
                    .IsRequired();
                stock
                    .Property(s => s.MinStockQty)
                    .HasColumnName("min_stock_qty")
                    .HasColumnType("numeric(16,6)");
                stock
                    .Property(s => s.MaxStockQty)
                    .HasColumnName("max_stock_qty")
                    .HasColumnType("numeric(16,6)");
            }
        );

        // ── MasterEntity fields ────────────────────────────────────────────
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Collections (explicit FK — avoids shadow property concurrency issues) ──
        builder
            .HasMany(x => x.Variants)
            .WithOne()
            .HasPrincipalKey(x => new { x.Id, x.TenantId, x.CompanyId })
            .HasForeignKey("ItemId", "TenantId", "CompanyId")
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(nameof(ItemImage.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.UnitConversions)
            .WithOne()
            .HasForeignKey(nameof(ItemUnitConversion.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Substitutes)
            .WithOne()
            .HasForeignKey(nameof(ItemSubstitute.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.PackagingLevels)
            .WithOne()
            .HasPrincipalKey(x => new { x.Id, x.TenantId, x.CompanyId })
            .HasForeignKey("ItemId", "TenantId", "CompanyId")
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.SupplierCodes)
            .WithOne()
            .HasPrincipalKey(x => new { x.Id, x.TenantId, x.CompanyId })
            .HasForeignKey("ItemId", "TenantId", "CompanyId")
            .OnDelete(DeleteBehavior.Cascade);

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2)
        builder
            .HasMany(x => x.SpecialTaxConfigurations)
            .WithOne()
            .HasForeignKey(nameof(ItemSpecialTaxConfiguration.ItemId))
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_items_subscriber");

        builder
            .HasIndex(x => new { x.TenantId, x.ItemTypeId })
            .HasDatabaseName("ix_items_subscriber_type");

        builder
            .HasIndex(x => new { x.TenantId, x.CategoryNodeId })
            .HasDatabaseName("ix_items_subscriber_category");

        // UNIQUE(tenant_id, company_id, sku) is created explicitly by the A1 migration
        // because SKU belongs to the table-split ItemCode owned value object.

        // ── Integridad referencial de clasificación ─────────────────────────
        builder
            .HasOne<ItemCategoryNode>()
            .WithMany()
            .HasForeignKey(x => x.CategoryNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Brand>()
            .WithMany()
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── FK física a item_types(id) ────────────────────────────────────────
        builder
            .HasOne<ItemTypeDefinition>()
            .WithMany()
            .HasForeignKey(x => x.ItemTypeId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
