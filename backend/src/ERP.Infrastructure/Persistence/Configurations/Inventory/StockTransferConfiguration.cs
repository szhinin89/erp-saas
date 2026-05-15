using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfer");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").IsRequired();
        builder.Property(e => e.TransferNumber).HasColumnName("transfer_number").HasMaxLength(StockTransfer.NumberMaxLen).IsRequired();
        builder.Property(e => e.SourceWarehouseId).HasColumnName("source_warehouse_id").IsRequired();
        builder.Property(e => e.TargetWarehouseId).HasColumnName("target_warehouse_id").IsRequired();
        builder.Property(e => e.TransferDate).HasColumnName("transfer_date").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(StockTransfer.StatusMaxLen).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(StockTransfer.ReasonMaxLen);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(StockTransfer.NotesMaxLen);
        builder.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.SourceWarehouse).WithMany().HasForeignKey(e => e.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TargetWarehouse).WithMany().HasForeignKey(e => e.TargetWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.StockTransferId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.TransferNumber }).IsUnique().HasDatabaseName("uq_stock_transfer_number");
        builder.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("ix_stock_transfer_tenant_status");
    }
}
