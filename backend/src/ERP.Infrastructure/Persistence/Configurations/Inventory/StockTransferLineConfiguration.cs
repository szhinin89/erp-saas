using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("stock_transfer_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.StockTransferId).HasColumnName("stock_transfer_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder
            .Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(StockTransferLine.DescriptionMaxLen)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => x.StockTransferId)
            .HasDatabaseName("ix_stock_transfer_lines_transfer");
    }
}
