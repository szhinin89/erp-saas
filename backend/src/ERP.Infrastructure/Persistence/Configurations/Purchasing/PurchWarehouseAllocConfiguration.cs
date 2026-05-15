using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchWarehouseAllocConfiguration : IEntityTypeConfiguration<PurchWarehouseAlloc>
{
    public void Configure(EntityTypeBuilder<PurchWarehouseAlloc> builder)
    {
        builder.ToTable("purch_warehouse_alloc");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.PurchBillId).HasColumnName("purch_bill_id").IsRequired();
        builder.Property(a => a.PurchBillLineId).HasColumnName("purch_bill_line_id").IsRequired();
        builder.Property(a => a.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(a => a.ProductId).HasColumnName("product_id");
        builder.Property(a => a.Quantity).HasColumnName("quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(a => new { a.TenantId, a.PurchBillId }).HasDatabaseName("ix_purch_warehouse_alloc_bill_id");
    }
}
