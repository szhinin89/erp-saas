using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class SerialNumberConfiguration : IEntityTypeConfiguration<SerialNumber>
{
    public void Configure(EntityTypeBuilder<SerialNumber> builder)
    {
        builder.ToTable("serial_numbers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Serial).HasColumnName("serial").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.LotId).HasColumnName("lot_id");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.AcquiredAt).HasColumnName("acquired_at").IsRequired();
        builder.Property(x => x.SoldAt).HasColumnName("sold_at");
        builder.Property(x => x.DocumentRef).HasColumnName("document_ref").HasMaxLength(50);
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
        builder.Property(x => x.ReceiptLineId).HasColumnName("receipt_line_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId, x.ItemId, x.Serial })
            .IsUnique()
            .HasDatabaseName("uq_serial_number");

        builder.HasIndex(x => new { x.SubscriberId, x.ItemId })
            .HasDatabaseName("ix_serial_numbers_subscriber_item");

        builder.HasIndex(x => new { x.SubscriberId, x.Status })
            .HasDatabaseName("ix_serial_numbers_status");
    }
}
