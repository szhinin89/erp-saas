using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReturnDetailConfiguration : IEntityTypeConfiguration<SalesReturnDetail>
{
    public void Configure(EntityTypeBuilder<SalesReturnDetail> builder)
    {
        builder.ToTable("sales_return_details");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ReturnId).HasColumnName("return_id").IsRequired();
        builder
            .Property(x => x.OriginalInvoiceDetailId)
            .HasColumnName("original_invoice_detail_id")
            .IsRequired();

        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(SalesReturnDetail.DescriptionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.SnapshotSku)
            .HasColumnName("snapshot_sku")
            .HasMaxLength(SalesReturnDetail.SkuMaxLen);
        builder
            .Property(x => x.SnapshotItemName)
            .HasColumnName("snapshot_item_name")
            .HasMaxLength(SalesReturnDetail.ItemNameMaxLen);

        builder
            .Property(x => x.UomCode)
            .HasColumnName("uom_code")
            .HasMaxLength(SalesReturnDetail.UomCodeMaxLen)
            .IsRequired();

        builder
            .Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder
            .Property(x => x.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.DiscountPct)
            .HasColumnName("discount_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasColumnType("numeric(18,6)")
            .IsRequired();

        builder
            .Property(x => x.VatCode)
            .HasColumnName("vat_code")
            .HasMaxLength(SalesReturnDetail.VatCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.VatRate)
            .HasColumnName("vat_rate")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.VatAmount)
            .HasColumnName("vat_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .Property(x => x.IceCode)
            .HasColumnName("ice_code")
            .HasMaxLength(SalesReturnDetail.IceCodeMaxLen);
        builder
            .Property(x => x.IceRate)
            .HasColumnName("ice_rate")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.IceAmount)
            .HasColumnName("ice_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.IsFrozen).HasColumnName("is_frozen").IsRequired();

        // ── Computed properties (NOT persisted) ─────────────────────
        builder.Ignore(x => x.LineSubtotal);
        builder.Ignore(x => x.TaxableBase);
        builder.Ignore(x => x.TaxInclusiveTotal);

        // ── Relationships ───────────────────────────────────────────
        builder
            .HasOne<SalesInvoiceDetail>()
            .WithMany()
            .HasForeignKey(x => x.OriginalInvoiceDetailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.ReturnId })
            .HasDatabaseName("ix_sales_return_details_tenant_return");

        // Fuente del cómputo de remanente devolvible (GetReturnedQuantityByInvoiceDetailAsync) —
        // suma Quantity por línea de factura original.
        builder
            .HasIndex(x => new { x.TenantId, x.OriginalInvoiceDetailId })
            .HasDatabaseName("ix_sales_return_details_tenant_original_line");
    }
}
