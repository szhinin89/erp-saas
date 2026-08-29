using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesInvoiceDetailConfiguration : IEntityTypeConfiguration<SalesInvoiceDetail>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceDetail> builder)
    {
        builder.ToTable("sales_invoice_details");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();

        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(SalesInvoiceDetail.DescriptionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.SnapshotSku)
            .HasColumnName("snapshot_sku")
            .HasMaxLength(SalesInvoiceDetail.SkuMaxLen);
        builder
            .Property(x => x.SnapshotItemName)
            .HasColumnName("snapshot_item_name")
            .HasMaxLength(SalesInvoiceDetail.ItemNameMaxLen);

        builder.Property(x => x.PackagingLevelId).HasColumnName("packaging_level_id");
        builder
            .Property(x => x.UomCode)
            .HasColumnName("uom_code")
            .HasMaxLength(SalesInvoiceDetail.UomCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.BaseUomCode)
            .HasColumnName("base_uom_code")
            .HasMaxLength(SalesInvoiceDetail.UomCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.ConversionFactor)
            .HasColumnName("conversion_factor")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.QuantityInBaseUom)
            .HasColumnName("quantity_in_base_uom")
            .HasColumnType("numeric(18,4)")
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
            .HasMaxLength(SalesInvoiceDetail.VatCodeMaxLen)
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
            .HasMaxLength(SalesInvoiceDetail.VatNameMaxLen);

        builder
            .Property(x => x.IceCode)
            .HasColumnName("ice_code")
            .HasMaxLength(SalesInvoiceDetail.IceCodeMaxLen);
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
            .HasMaxLength(SalesInvoiceDetail.IceNameMaxLen);
        builder
            .Property(x => x.IceCalculationType)
            .HasColumnName("ice_calculation_type")
            .HasConversion<int>()
            .IsRequired();

        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(SalesInvoiceDetail.NotesMaxLen);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsFrozen).HasColumnName("is_frozen").IsRequired();

        builder.Ignore(x => x.LineSubtotal);
        builder.Ignore(x => x.TaxableBase);
        builder.Ignore(x => x.TaxInclusiveTotal);
        builder.Ignore(x => x.IrbpnrCode);
        builder.Ignore(x => x.IrbpnrRate);
        builder.Ignore(x => x.SnapshotIrbpnrName);
        builder.Ignore(x => x.IrbpnrAmount);

        builder
            .HasIndex(x => new { x.TenantId, x.InvoiceId })
            .HasDatabaseName("ix_sales_invoice_details_tenant_invoice");

        builder
            .HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3)
        builder
            .HasMany(x => x.Taxes)
            .WithOne()
            .HasForeignKey(x => x.SalesInvoiceDetailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
