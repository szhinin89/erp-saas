using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>Mapeo EF de <see cref="PurchaseReturnDetail"/> — diseño P0-02 §7.2, Fase 2.</summary>
public sealed class PurchaseReturnDetailConfiguration
    : IEntityTypeConfiguration<PurchaseReturnDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnDetail> builder)
    {
        builder.ToTable("purchase_return_details");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PurchaseReturnId).HasColumnName("purchase_return_id").IsRequired();
        builder
            .Property(x => x.OriginalInvoiceDetailId)
            .HasColumnName("original_invoice_detail_id")
            .IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder
            .Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();

        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(18,6)");
        builder.Property(x => x.VatCode).HasColumnName("vat_code").HasMaxLength(20);
        builder.Property(x => x.VatRate).HasColumnName("vat_rate").HasColumnType("numeric(5,2)");
        builder
            .Property(x => x.ReturnedSubtotal)
            .HasColumnName("returned_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ReturnedDiscountAmount)
            .HasColumnName("returned_discount_amount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ReturnedVatAmount)
            .HasColumnName("returned_vat_amount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.HistoricalCostAmount)
            .HasColumnName("historical_cost_amount")
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.IsFrozen).HasColumnName("is_frozen").IsRequired();

        // ── Computed properties (NOT persisted) ─────────────────────
        builder.Ignore(x => x.LineGrandTotal);
        builder.Ignore(x => x.IrbpnrAmount);
        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Fase 3) — Ice*/ReturnedIceAmount pasan a
        // legacy compatibility mirror computado desde _taxes (mismo tratamiento que IrbpnrAmount
        // arriba). Las columnas físicas ice_code/ice_rate/returned_ice_amount NO se eliminan en
        // esta fase (ADR-032 §7, Fase 7 pendiente) — quedan huérfanas, ya no mapeadas.
        builder.Ignore(x => x.IceCode);
        builder.Ignore(x => x.IceRate);
        builder.Ignore(x => x.ReturnedIceAmount);

        // ── Relationships ────────────────────────────────────────────
        builder
            .HasOne<PurchaseInvoiceDetail>()
            .WithMany()
            .HasForeignKey(x => x.OriginalInvoiceDetailId)
            .OnDelete(DeleteBehavior.Restrict);

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-1)
        builder
            .HasMany(x => x.Taxes)
            .WithOne()
            .HasForeignKey(x => x.PurchaseReturnDetailId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseReturnId })
            .HasDatabaseName("ix_purchase_return_details_tenant_purchase_return");

        builder
            .HasIndex(x => new { x.PurchaseReturnId, x.OriginalInvoiceDetailId })
            .IsUnique()
            .HasDatabaseName("uq_purchase_return_details_return_original_line");

        // Fuente del cómputo de remanente devolvible — suma Quantity por línea de factura original.
        builder
            .HasIndex(x => new { x.TenantId, x.OriginalInvoiceDetailId })
            .HasDatabaseName("ix_purchase_return_details_tenant_original_line");
    }
}
