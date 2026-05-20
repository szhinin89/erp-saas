using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesDetailConfiguration : IEntityTypeConfiguration<SalesDetail>
{
    public void Configure(EntityTypeBuilder<SalesDetail> builder)
    {
        builder.ToTable("sales_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SalesDocumentId).HasColumnName("sales_document_id").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id");
        builder.Property(e => e.ProductCode).HasColumnName("product_code").HasMaxLength(100);
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.UnitCode).HasColumnName("unit_code").HasMaxLength(20);
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
        builder.Property(e => e.DiscountPct).HasColumnName("discount_pct").HasPrecision(6, 2);
        builder.Property(e => e.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 4);
        builder.Property(e => e.VatCode).HasColumnName("vat_code").HasMaxLength(5);
        builder.Property(e => e.VatPercentage).HasColumnName("vat_percentage").HasPrecision(6, 2);
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 4);
        builder.Property(e => e.IceAmount).HasColumnName("ice_amount").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);
        builder.Property(e => e.SortOrder).HasColumnName("sort_order");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.SalesDocumentId).HasDatabaseName("ix_sales_detail_document");
    }
}
