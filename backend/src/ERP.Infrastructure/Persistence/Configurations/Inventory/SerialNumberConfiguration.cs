using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class SerialNumberConfiguration : IEntityTypeConfiguration<SerialNumber>
{
    public void Configure(EntityTypeBuilder<SerialNumber> builder)
    {
        builder.ToTable("inventory_serials");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.Serial).HasColumnName("serial").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(e => e.VariantId).HasColumnName("variant_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(e => e.LotId).HasColumnName("lot_id");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<short>().IsRequired();
        builder.Property(e => e.AcquiredAt).HasColumnName("acquired_at").IsRequired();
        builder.Property(e => e.SoldAt).HasColumnName("sold_at");
        builder.Property(e => e.DocumentRef).HasColumnName("document_ref").HasMaxLength(100);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(e => e.ReceiptLineId).HasColumnName("receipt_line_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.ItemId }).HasDatabaseName("ix_inv_serials_tenant_company_item");
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.ItemId, e.Serial })
               .IsUnique().HasDatabaseName("uq_inv_serials_item_serial");
    }
}
