using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.MovementType).HasColumnName("movement_type")
            .HasConversion<int>().IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.UomCode).HasColumnName("uom_code")
            .HasMaxLength(StockMovement.UomCodeMaxLen).IsRequired();
        builder.Property(x => x.PreviousQuantity).HasColumnName("previous_quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.ResultQuantity).HasColumnName("result_quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.SequenceNumber).HasColumnName("sequence_number").IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference")
            .HasMaxLength(StockMovement.ReferenceMaxLen);
        builder.Property(x => x.SourceDocId).HasColumnName("source_doc_id");
        builder.Property(x => x.SourceDocType).HasColumnName("source_doc_type")
            .HasMaxLength(StockMovement.SourceDocTypeMaxLen);
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost")
            .HasColumnType("numeric(18,6)");
        builder.Property(x => x.TotalCost).HasColumnName("total_cost")
            .HasColumnType("numeric(18,6)");
        builder.Property(x => x.RunningAverageCost).HasColumnName("running_average_cost")
            .HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.RunningStockValue).HasColumnName("running_stock_value")
            .HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.EffectiveDate).HasColumnName("effective_date")
            .HasColumnType("date").IsRequired();

        builder.Property(x => x.LotId).HasColumnName("lot_id");
        builder.Property(x => x.SerialId).HasColumnName("serial_id");
        builder.Property(x => x.AccountingTransactionId).HasColumnName("accounting_transaction_id");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_stock_movements_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.WarehouseId })
            .HasDatabaseName("ix_stock_movements_tenant_product_warehouse");

        // Segunda línea de defensa contra corrupción del Kardex bajo concurrencia:
        // la BD nunca permite dos movimientos con el mismo correlativo para la misma
        // clave, incluso si el chequeo optimista (xmin de CurrentStock) fallara en detectarlo.
        builder.HasIndex(x => new { x.CompanyId, x.ProductId, x.WarehouseId, x.SequenceNumber })
            .IsUnique()
            .HasDatabaseName("uq_stock_movements_company_product_warehouse_sequence");

        builder.HasIndex(x => new { x.SourceDocId, x.SourceDocType })
            .HasDatabaseName("ix_stock_movements_source_doc");

        builder.HasIndex(x => new { x.CompanyId, x.EffectiveDate })
            .HasDatabaseName("ix_stock_movements_company_effective_date");

        builder.HasIndex(x => new { x.CompanyId, x.CreatedBy, x.CreatedAt })
            .HasDatabaseName("ix_stock_movements_company_created_by_created_at");

        builder.HasIndex(x => new { x.CompanyId, x.BranchId })
            .HasDatabaseName("ix_stock_movements_company_branch");
    }
}
