using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movement");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(m => m.CompanyId).HasColumnName("company_id");
        builder.Property(m => m.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(m => m.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(m => m.MovementType).HasColumnName("movement_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(m => m.Quantity).HasColumnName("quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(m => m.PreviousQuantity).HasColumnName("previous_quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(m => m.ResultQuantity).HasColumnName("result_quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(m => m.Reference).HasColumnName("reference").HasMaxLength(StockMovement.ReferenceMaxLen);
        builder.Property(m => m.SourceDocId).HasColumnName("source_doc_id");
        builder.Property(m => m.SourceDocType).HasColumnName("source_doc_type").HasMaxLength(StockMovement.SourceDocTypeMaxLen);
        builder.Property(m => m.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 6);
        builder.Property(m => m.TotalCost).HasColumnName("total_cost").HasPrecision(18, 6);
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(m => new { m.SubscriberId, m.ProductId, m.WarehouseId }).HasDatabaseName("ix_stock_movement_subscriber_product_warehouse");
        builder.HasIndex(m => new { m.SubscriberId, m.MovementType }).HasDatabaseName("ix_stock_movement_subscriber_type");
        builder.HasIndex(m => m.SourceDocId).HasDatabaseName("ix_stock_movement_source_doc");
    }
}
