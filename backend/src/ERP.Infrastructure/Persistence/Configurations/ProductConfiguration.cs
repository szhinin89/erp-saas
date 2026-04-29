using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

        // ── Impuestos ─────────────────────────────────────────────
        builder.Property(p => p.SaleTaxId).HasColumnName("sale_tax_id").IsRequired();
        builder.Property(p => p.PurchaseTaxId).HasColumnName("purchase_tax_id").IsRequired();
        builder.Property(p => p.ExciseTaxId).HasColumnName("excise_tax_id");

        // ── Comportamiento de stock ───────────────────────────────
        builder.Property(p => p.IsService).HasColumnName("is_service").IsRequired();
        builder.Property(p => p.TracksLot).HasColumnName("tracks_lot").IsRequired();
        builder.Property(p => p.TracksSeries).HasColumnName("tracks_series").IsRequired();
        builder.Property(p => p.HasRecipe).HasColumnName("has_recipe").IsRequired();
        builder.Property(p => p.StockWithDecimal).HasColumnName("stock_with_decimal").IsRequired();

        // ── Canales ───────────────────────────────────────────────
        builder.Property(p => p.AvailableOnWeb).HasColumnName("available_on_web").IsRequired();
        builder.Property(p => p.AvailableOnMobile).HasColumnName("available_on_mobile").IsRequired();
        builder.Property(p => p.IsFavorite).HasColumnName("is_favorite").IsRequired();
        builder.Property(p => p.IsForSale).HasColumnName("is_for_sale").IsRequired();

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
            b.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_barcodes_product_id");
        });
    }
}