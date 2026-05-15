using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Contabilidad.Entities;
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
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();

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
        builder.Property(p => p.UnitOfMeasureId).HasColumnName("unit_of_measure_id").IsRequired();
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

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(p => p.UnitOfMeasureId)
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

        builder.Property(p => p.SaleTaxId).HasColumnName("sale_tax_id");
        builder.Property(p => p.PurchaseTaxId).HasColumnName("purchase_tax_id");
        builder.Property(p => p.ExciseTaxId).HasColumnName("excise_tax_id");

        builder.Property(p => p.SaleVatAccountId).HasColumnName("sale_vat_account_id");
        builder.Property(p => p.PurchaseVatAccountId).HasColumnName("purchase_vat_account_id");
        builder.Property(p => p.ExciseAccountId).HasColumnName("excise_account_id");

        builder.HasOne<TaxRate>()
            .WithMany()
            .HasForeignKey(p => p.SaleTaxId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TaxRate>()
            .WithMany()
            .HasForeignKey(p => p.PurchaseTaxId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TaxRate>()
            .WithMany()
            .HasForeignKey(p => p.ExciseTaxId)
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

        // ── Variantes ─────────────────────────────────────────────
        builder.Property(p => p.BaseColor).HasColumnName("base_color").HasMaxLength(80);
        builder.Property(p => p.HasMultipleColors).HasColumnName("has_multiple_colors").IsRequired();
        builder.Property(p => p.HasSizes).HasColumnName("has_sizes").IsRequired();

        // ── Aranceles / importación ───────────────────────────────
        builder.Property(p => p.HandlesTariff).HasColumnName("handles_tariff").IsRequired();

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
        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("ix_products_tenant_id");

        builder.HasIndex(p => new { p.TenantId, p.SaleCode })
            .IsUnique()
            .HasDatabaseName("ix_products_tenant_sale_code");

        builder.HasIndex(p => new { p.TenantId, p.ShortName })
            .HasDatabaseName("ix_products_tenant_short_name");

        // ── Códigos de barras (owned collection) ──────────────────
        builder.OwnsMany(p => p.Barcodes, b =>
        {
            b.ToTable("product_barcodes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            b.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
            b.Property(x => x.Type).HasColumnName("type").IsRequired();
            b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            b.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_barcodes_product_id");
            b.HasIndex(x => new { x.TenantId, x.Code }).HasDatabaseName("ix_product_barcodes_tenant_code");
        });

        builder.OwnsMany(p => p.SupplierCodes, s =>
        {
            s.ToTable("product_supplier_codes");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).HasColumnName("id");
            s.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            s.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            s.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
            s.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
            s.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
            s.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            s.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_supplier_codes_product_id");
            s.HasIndex(x => new { x.TenantId, x.SupplierId }).HasDatabaseName("ix_product_supplier_codes_tenant_supplier");
            s.HasIndex(x => new { x.TenantId, x.Code }).HasDatabaseName("ix_product_supplier_codes_tenant_code");
        });

        builder.OwnsMany(p => p.UnitConversions, u =>
        {
            u.ToTable("product_unit_conversions");
            u.HasKey(x => x.Id);
            u.Property(x => x.Id).HasColumnName("id");
            u.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            u.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            u.Property(x => x.AlternateUnitId).HasColumnName("alternate_unit_id").IsRequired();
            u.Property(x => x.ConversionFactor).HasColumnName("conversion_factor").HasPrecision(18, 6).IsRequired();
            u.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            u.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_unit_conversions_product_id");
            u.HasIndex(x => new { x.TenantId, x.AlternateUnitId }).HasDatabaseName("ix_product_unit_conversions_tenant_alt_unit");
        });

        builder.OwnsMany(p => p.Colors, c =>
        {
            c.ToTable("product_colors");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).HasColumnName("id");
            c.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            c.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            c.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            c.Property(x => x.HexCode).HasColumnName("hex_code").HasMaxLength(10);
            c.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            c.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_colors_product_id");
            c.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("ix_product_colors_tenant_name");
        });

        builder.OwnsMany(p => p.Sizes, s =>
        {
            s.ToTable("product_sizes");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).HasColumnName("id");
            s.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            s.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            s.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            s.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            s.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            s.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_sizes_product_id");
            s.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("ix_product_sizes_tenant_name");
        });

        builder.OwnsMany(p => p.Dimensions, d =>
        {
            d.ToTable("product_dimensions");
            d.HasKey(x => x.Id);
            d.Property(x => x.Id).HasColumnName("id");
            d.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            d.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            d.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            d.Property(x => x.Value).HasColumnName("value").HasMaxLength(80).IsRequired();
            d.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20).IsRequired();
            d.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            d.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_dimensions_product_id");
            d.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("ix_product_dimensions_tenant_name");
        });

        builder.OwnsMany(p => p.Images, i =>
        {
            i.ToTable("product_images");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).HasColumnName("id");
            i.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            i.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            i.Property(x => x.Url).HasColumnName("url").HasMaxLength(1024).IsRequired();
            i.Property(x => x.AltText).HasColumnName("alt_text").HasMaxLength(254);
            i.Property(x => x.IsMain).HasColumnName("is_main").IsRequired();
            i.Property(x => x.IsEcommerce).HasColumnName("is_ecommerce").IsRequired();
            i.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            i.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            i.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_images_product_id");
            i.HasIndex(x => new { x.TenantId, x.IsMain }).HasDatabaseName("ix_product_images_tenant_is_main");
            i.HasIndex(x => new { x.TenantId, x.IsEcommerce }).HasDatabaseName("ix_product_images_tenant_is_ecommerce");
        });

        builder.OwnsMany(p => p.Features, f =>
        {
            f.ToTable("product_features");
            f.HasKey(x => x.Id);
            f.Property(x => x.Id).HasColumnName("id");
            f.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            f.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            f.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            f.Property(x => x.Value).HasColumnName("value").HasMaxLength(254).IsRequired();
            f.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            f.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_features_product_id");
            f.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("ix_product_features_tenant_name");
        });

        builder.OwnsMany(p => p.TariffDetails, t =>
        {
            t.ToTable("product_tariff_details");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id");
            t.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            t.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            t.Property(x => x.OriginCountry).HasColumnName("origin_country").HasMaxLength(3).IsRequired();
            t.Property(x => x.TariffCode).HasColumnName("tariff_code").HasMaxLength(50).IsRequired();
            t.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(9, 2).IsRequired();
            t.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            t.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_tariff_details_product_id");
            t.HasIndex(x => new { x.TenantId, x.OriginCountry }).HasDatabaseName("ix_product_tariff_details_tenant_country");
        });

        builder.OwnsMany(p => p.Substitutes, s =>
        {
            s.ToTable("product_substitutes");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).HasColumnName("id");
            s.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            s.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            s.Property(x => x.SubstituteProductId).HasColumnName("substitute_product_id").IsRequired();
            s.Property(x => x.Note).HasColumnName("note").HasMaxLength(254);
            s.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            s.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_substitutes_product_id");
            s.HasIndex(x => new { x.TenantId, x.SubstituteProductId }).HasDatabaseName("ix_product_substitutes_tenant_substitute");
        });

        builder.OwnsMany(p => p.CustomFields, c =>
        {
            c.ToTable("product_custom_fields");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).HasColumnName("id");
            c.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            c.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            c.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(120).IsRequired();
            c.Property(x => x.FieldType).HasColumnName("field_type").IsRequired();
            c.Property(x => x.FieldValue).HasColumnName("field_value").HasMaxLength(1024).IsRequired();
            c.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            c.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_custom_fields_product_id");
            c.HasIndex(x => new { x.TenantId, x.FieldName }).HasDatabaseName("ix_product_custom_fields_tenant_field_name");
        });
    }
}