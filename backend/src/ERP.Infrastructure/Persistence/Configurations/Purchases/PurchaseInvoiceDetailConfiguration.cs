using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseInvoiceDetailConfiguration
    : IEntityTypeConfiguration<PurchaseInvoiceDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceDetail> builder)
    {
        builder.ToTable("purchase_invoice_details");

        // ── Identity ────────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();

        // ── Product Snapshot ────────────────────────────────────────────
        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PurchaseInvoiceDetail.DescriptionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.SnapshotSku)
            .HasColumnName("snapshot_sku")
            .HasMaxLength(PurchaseInvoiceDetail.SkuMaxLen);
        builder
            .Property(x => x.SnapshotItemName)
            .HasColumnName("snapshot_item_name")
            .HasMaxLength(PurchaseInvoiceDetail.ItemNameMaxLen);
        builder
            .Property(x => x.SnapshotSupplierCode)
            .HasColumnName("snapshot_supplier_code")
            .HasMaxLength(PurchaseInvoiceDetail.SupplierCodeMaxLen);

        // ── UoM ─────────────────────────────────────────────────────────
        builder
            .Property(x => x.UomCode)
            .HasColumnName("uom_code")
            .HasMaxLength(PurchaseInvoiceDetail.UomCodeMaxLen)
            .HasDefaultValue("UNIT")
            .IsRequired();
        builder
            .Property(x => x.ConversionFactor)
            .HasColumnName("conversion_factor")
            .HasColumnType("numeric(18,6)")
            .HasDefaultValue(1m)
            .IsRequired();
        builder
            .Property(x => x.QuantityInBaseUom)
            .HasColumnName("quantity_in_base_uom")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        // ── Quantity & Price ────────────────────────────────────────────
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

        // ── Distributed Costs ──────────────────────────────────────────
        builder
            .Property(x => x.FreightAllocated)
            .HasColumnName("freight_allocated")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.OtherCostsAllocated)
            .HasColumnName("other_costs_allocated")
            .HasColumnType("numeric(18,6)")
            .IsRequired();

        // ── Landed Cost (frozen on Confirm) ────────────────────────────
        builder
            .Property(x => x.TotalLineCost)
            .HasColumnName("total_line_cost")
            .HasColumnType("numeric(18,6)")
            .HasDefaultValue(0m)
            .IsRequired();
        builder
            .Property(x => x.LandedUnitCost)
            .HasColumnName("landed_unit_cost")
            .HasColumnType("numeric(18,6)")
            .HasDefaultValue(0m)
            .IsRequired();
        builder
            .Property(x => x.IsFrozen)
            .HasColumnName("is_frozen")
            .HasDefaultValue(false)
            .IsRequired();

        // ── VAT (fiscal snapshot) ───────────────────────────────────────
        builder
            .Property(x => x.VatCode)
            .HasColumnName("vat_code")
            .HasMaxLength(PurchaseInvoiceDetail.VatCodeMaxLen)
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
            .Property(x => x.SnapshotVatName)
            .HasColumnName("snapshot_vat_name")
            .HasMaxLength(PurchaseInvoiceDetail.VatNameMaxLen);

        // ── ICE (fiscal snapshot) ───────────────────────────────────────
        builder
            .Property(x => x.IceCode)
            .HasColumnName("ice_code")
            .HasMaxLength(PurchaseInvoiceDetail.IceCodeMaxLen);
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
        builder
            .Property(x => x.SnapshotIceName)
            .HasColumnName("snapshot_ice_name")
            .HasMaxLength(PurchaseInvoiceDetail.IceNameMaxLen);

        // ── Warehouse (logistic reference) ──────────────────────────────
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
        builder
            .Property(x => x.SnapshotWarehouseCode)
            .HasColumnName("snapshot_warehouse_code")
            .HasMaxLength(PurchaseInvoiceDetail.WarehouseCodeMaxLen);

        // ── Analytic Snapshot ───────────────────────────────────────────
        builder
            .Property(x => x.SnapshotItemPvp)
            .HasColumnName("snapshot_item_pvp")
            .HasColumnType("numeric(18,6)")
            .IsRequired();

        // ── Purchase Order Traceability ─────────────────────────────────
        builder.Property(x => x.PurchaseOrderDetailId).HasColumnName("purchase_order_detail_id");
        builder
            .Property(x => x.OrderedQuantity)
            .HasColumnName("ordered_quantity")
            .HasColumnType("numeric(18,4)");

        // ── Purchase Reception Traceability ──────────────────────────────
        builder
            .Property(x => x.PurchaseReceptionLineId)
            .HasColumnName("purchase_reception_line_id");

        // ── Meta ────────────────────────────────────────────────────────
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(PurchaseInvoiceDetail.NotesMaxLen);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        // ── Ignored (computed in domain, not persisted) ─────────────────
        builder.Ignore(x => x.LineSubtotal);
        builder.Ignore(x => x.TaxableBase);
        builder.Ignore(x => x.TaxInclusiveTotal);

        // ── Relationships ───────────────────────────────────────────────
        builder
            .HasOne<Item>()
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────────
        builder.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_purchase_invoice_details_invoice");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_purchase_invoice_details_tenant");

        builder
            .HasIndex(x => new { x.TenantId, x.ItemId })
            .HasDatabaseName("ix_purchase_invoice_details_tenant_item");

        builder
            .HasIndex(x => x.PurchaseOrderDetailId)
            .HasDatabaseName("ix_purchase_invoice_details_po_detail")
            .HasFilter("purchase_order_detail_id IS NOT NULL");

        builder
            .HasIndex(x => x.PurchaseReceptionLineId)
            .HasDatabaseName("ix_purchase_invoice_details_reception_line")
            .HasFilter("purchase_reception_line_id IS NOT NULL");
    }
}
