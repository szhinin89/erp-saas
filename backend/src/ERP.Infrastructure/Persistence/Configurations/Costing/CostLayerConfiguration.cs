using ERP.Domain.Modules.Costing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Costing;

public sealed class CostLayerConfiguration : IEntityTypeConfiguration<CostLayer>
{
    public void Configure(EntityTypeBuilder<CostLayer> builder)
    {
        builder.ToTable("cost_layers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.VariantId).HasColumnName("variant_id");
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.LotId).HasColumnName("lot_id");
        builder.Property(x => x.CostMethod)
            .HasColumnName("cost_method")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(x => x.LayerDate).HasColumnName("layer_date").IsRequired();
        builder.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
        builder.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntryQty).HasColumnName("entry_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.TotalCost).HasColumnName("total_cost").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.RemainingQty).HasColumnName("remaining_qty").HasPrecision(14, 4).IsRequired();
        builder.Property(x => x.IsExhausted).HasColumnName("is_exhausted").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Critical FIFO index — ordered by layer_date for sequential FIFO consumption
        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId, x.ItemId, x.WarehouseId, x.IsExhausted, x.LayerDate })
            .HasDatabaseName("ix_cost_layers_fifo");

        builder.HasIndex(x => new { x.SubscriberId, x.ItemId })
            .HasDatabaseName("ix_cost_layers_item");
    }
}
