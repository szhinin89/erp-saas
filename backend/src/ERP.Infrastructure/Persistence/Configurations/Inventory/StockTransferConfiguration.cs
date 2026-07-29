using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.Sequential).HasColumnName("sequential").IsRequired();
        builder
            .Property(x => x.TransferNumber)
            .HasColumnName("transfer_number")
            .HasMaxLength(StockTransfer.NumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.OperationBranchId)
            .HasColumnName("operation_branch_id")
            .IsRequired();
        builder
            .Property(x => x.SourceWarehouseId)
            .HasColumnName("source_warehouse_id")
            .IsRequired();
        builder
            .Property(x => x.TargetWarehouseId)
            .HasColumnName("target_warehouse_id")
            .IsRequired();
        builder.Property(x => x.TransferDate).HasColumnName("transfer_date").IsRequired();
        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(StockTransfer.StatusMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(StockTransfer.ReasonMaxLen);
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(StockTransfer.NotesMaxLen);
        builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(x => x.ConfirmedBy).HasColumnName("confirmed_by");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.SourceWarehouse)
            .WithMany()
            .HasForeignKey(x => x.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(x => x.TargetWarehouse)
            .WithMany()
            .HasForeignKey(x => x.TargetWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.OperationBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_stock_transfers_tenant_company");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.TransferNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_stock_transfers_tenant_company_number");
    }
}
