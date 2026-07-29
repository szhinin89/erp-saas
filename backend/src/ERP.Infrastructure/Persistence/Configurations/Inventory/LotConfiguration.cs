using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("inventory_lots");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(e => e.LotNumber)
            .HasColumnName("lot_number")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(e => e.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(e => e.VariantId).HasColumnName("variant_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(e => e.ManufactureDate).HasColumnName("manufacture_date");
        builder.Property(e => e.ExpirationDate).HasColumnName("expiration_date");
        builder
            .Property(e => e.InitialQty)
            .HasColumnName("initial_qty")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(e => e.CurrentQty)
            .HasColumnName("current_qty")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<short>().IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(e => e.ReceiptLineId).HasColumnName("receipt_line_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.ItemId,
            })
            .HasDatabaseName("ix_inv_lots_tenant_company_item");
        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.ItemId,
                e.LotNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_inv_lots_item_number");
    }
}
