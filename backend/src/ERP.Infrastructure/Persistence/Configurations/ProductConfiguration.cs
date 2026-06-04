using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Products.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        // ── Clave y tenant ────────────────────────────────────────
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(p => p.CompanyId).HasColumnName("company_id");
        builder.HasIndex(p => new { p.SubscriberId, p.CompanyId }).HasDatabaseName("ix_products_subscriber_company");

        // ── Identificación ────────────────────────────────────────
        builder.Property(p => p.SaleCode).HasColumnName("sale_code").HasMaxLength(50).IsRequired();
        builder.Property(p => p.PurchaseCode).HasColumnName("purchase_code").HasMaxLength(50);
        builder.Property(p => p.ShortName).HasColumnName("short_name").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(254).IsRequired();
        builder.Property(p => p.Observations).HasColumnName("observations");

        // ── Categorización ────────────────────────────────────────
        builder.Property(p => p.LineId).HasColumnName("line_id").IsRequired();
        builder.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(p => p.SubcategoryId).HasColumnName("subcategory_id").IsRequired();

        // ── Catálogos ─────────────────────────────────────────────
        builder.Property(p => p.UomCode).HasColumnName("uom_code").HasMaxLength(10).IsRequired();
        builder.Property(p => p.BrandId).HasColumnName("brand_id").IsRequired();
        builder.Property(p => p.ProductTypeId).HasColumnName("product_type_id").IsRequired();
        builder.Property(p => p.TariffId).HasColumnName("tariff_id").IsRequired();

        // ── Relaciones (FKs) ───────────────────────────────────────
        builder.HasOne<ProductLine>()
            .WithMany()
            .HasForeignKey(p => p.LineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductSubcategory>()
            .WithMany()
            .HasForeignKey(p => p.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK a global.sri_uom.Code — fuente única de UOM
        builder.HasOne<SriUom>()
            .WithMany()
            .HasForeignKey(p => p.UomCode)
            .HasPrincipalKey(u => u.Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductType>()
            .WithMany()
            .HasForeignKey(p => p.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tariff>()
            .WithMany()
            .HasForeignKey(p => p.TariffId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Impuestos ─────────────────────────────────────────────
        builder.Property(p => p.AppliesVatOnSale).HasColumnName("applies_vat_on_sale").IsRequired();
        builder.Property(p => p.AppliesVatOnPurchase).HasColumnName("applies_vat_on_purchase").IsRequired();
        builder.Property(p => p.AppliesExciseTax).HasColumnName("applies_excise_tax").IsRequired();

        // FK a global.sri_vat_rate.Code / global.sri_ice_rate.Code
        builder.Property(p => p.SaleVatCode).HasColumnName("sale_vat_code").HasMaxLength(10);
        builder.Property(p => p.PurchaseVatCode).HasColumnName("purchase_vat_code").HasMaxLength(10);
        builder.Property(p => p.IceCode).HasColumnName("ice_code").HasMaxLength(10);

        builder.Property(p => p.SaleVatAccountId).HasColumnName("sale_vat_account_id");
        builder.Property(p => p.PurchaseVatAccountId).HasColumnName("purchase_vat_account_id");
        builder.Property(p => p.ExciseAccountId).HasColumnName("excise_account_id");

        builder.HasOne<SriVatRate>()
            .WithMany()
            .HasForeignKey(p => p.SaleVatCode)
            .HasPrincipalKey(v => v.Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SriVatRate>()
            .WithMany()
            .HasForeignKey(p => p.PurchaseVatCode)
            .HasPrincipalKey(v => v.Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SriIceRate>()
            .WithMany()
            .HasForeignKey(p => p.IceCode)
            .HasPrincipalKey(v => v.Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.SaleVatAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.PurchaseVatAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.ExciseAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Comportamiento de stock ───────────────────────────────
        builder.Property(p => p.IsService).HasColumnName("is_service").IsRequired();
        builder.Property(p => p.TracksStock).HasColumnName("tracks_stock").IsRequired();
        builder.Property(p => p.TracksLot).HasColumnName("tracks_lot").IsRequired();
        builder.Property(p => p.TracksSeries).HasColumnName("tracks_series").IsRequired();
        builder.Property(p => p.HasRecipe).HasColumnName("has_recipe").IsRequired();
        builder.Property(p => p.StockWithDecimal).HasColumnName("stock_with_decimal").IsRequired();
        builder.Property(p => p.RecipeId).HasColumnName("recipe_id");
        builder.Property(p => p.SaleWithDecimal).HasColumnName("sale_with_decimal").IsRequired();
        builder.Property(p => p.MaxItemDiscountPercent).HasColumnName("max_item_discount_percent").HasPrecision(9, 2).IsRequired();

        // ── Canales ───────────────────────────────────────────────
        builder.Property(p => p.AvailableOnWeb).HasColumnName("available_on_web").IsRequired();
        builder.Property(p => p.AvailableOnMobile).HasColumnName("available_on_mobile").IsRequired();
        builder.Property(p => p.IsEcommerceActive).HasColumnName("is_ecommerce_active").IsRequired();
        builder.Property(p => p.IsFavorite).HasColumnName("is_favorite").IsRequired();
        builder.Property(p => p.IsForSale).HasColumnName("is_for_sale").IsRequired();



        // ── SRI ───────────────────────────────────────────────────
        builder.Property(p => p.SriServiceCode).HasColumnName("sri_service_code").HasMaxLength(5);

        // ── Estado (MasterEntity) ─────────────────────────────────
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();

        // ── Auditoría ─────────────────────────────────────────────
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        // ── Índices ───────────────────────────────────────────────
        builder.HasIndex(p => p.SubscriberId)
            .HasDatabaseName("ix_products_subscriber_id");

        builder.HasIndex(p => new { p.SubscriberId, p.SaleCode })
            .IsUnique()
            .HasDatabaseName("ix_products_subscriber_sale_code");

        builder.HasIndex(p => new { p.SubscriberId, p.ShortName })
            .HasDatabaseName("ix_products_subscriber_short_name");

        // ── Códigos de barras (owned collection) ──────────────────
        builder.OwnsMany(p => p.Barcodes, b =>
        {
            b.ToTable("product_barcodes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
            b.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
            b.Property(x => x.Type).HasColumnName("type").IsRequired();
            b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            b.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_barcodes_product_id");
            b.HasIndex(x => new { x.SubscriberId, x.Code }).HasDatabaseName("ix_product_barcodes_subscriber_code");
        });

        builder.OwnsMany(p => p.UnitConversions, u =>
        {
            u.ToTable("product_unit_conversions");
            u.HasKey(x => x.Id);
            u.Property(x => x.Id).HasColumnName("id");
            u.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
            u.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            u.Property(x => x.AlternateUomCode).HasColumnName("alternate_uom_code").HasMaxLength(10).IsRequired();
            u.Property(x => x.ConversionFactor).HasColumnName("conversion_factor").HasPrecision(18, 6).IsRequired();
            u.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            u.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_unit_conversions_product_id");
            u.HasIndex(x => new { x.SubscriberId, x.AlternateUomCode }).HasDatabaseName("ix_product_unit_conversions_subscriber_alt_uom");
        });

        builder.OwnsMany(p => p.Images, i =>
        {
            i.ToTable("product_images");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).HasColumnName("id");
            i.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
            i.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            i.Property(x => x.Url).HasColumnName("url").HasMaxLength(1024).IsRequired();
            i.Property(x => x.AltText).HasColumnName("alt_text").HasMaxLength(254);
            i.Property(x => x.IsMain).HasColumnName("is_main").IsRequired();
            i.Property(x => x.IsEcommerce).HasColumnName("is_ecommerce").IsRequired();
            i.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            i.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            i.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_images_product_id");
            i.HasIndex(x => new { x.SubscriberId, x.IsMain }).HasDatabaseName("ix_product_images_subscriber_is_main");
            i.HasIndex(x => new { x.SubscriberId, x.IsEcommerce }).HasDatabaseName("ix_product_images_subscriber_is_ecommerce");
        });

        builder.OwnsMany(p => p.Substitutes, s =>
        {
            s.ToTable("product_substitutes");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).HasColumnName("id");
            s.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
            s.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            s.Property(x => x.SubstituteProductId).HasColumnName("substitute_product_id").IsRequired();
            s.Property(x => x.Note).HasColumnName("note").HasMaxLength(254);
            s.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            s.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_substitutes_product_id");
            s.HasIndex(x => new { x.SubscriberId, x.SubstituteProductId }).HasDatabaseName("ix_product_substitutes_subscriber_substitute");
        });

    }
}