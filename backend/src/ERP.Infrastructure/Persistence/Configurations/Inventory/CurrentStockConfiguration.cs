using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class CurrentStockConfiguration : IEntityTypeConfiguration<CurrentStock>
{
    public void Configure(EntityTypeBuilder<CurrentStock> builder)
    {
        builder.ToTable("current_stocks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.ReservedQuantity).HasColumnName("reserved_quantity")
            .HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.TotalStockValue).HasColumnName("total_stock_value")
            .HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.LastUpdatedAt).HasColumnName("last_updated_at").IsRequired();

        builder.Ignore(x => x.AvailableQuantity);
        builder.Ignore(x => x.AverageCost);
        builder.Ignore(x => x.RowVersion);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_current_stocks_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.WarehouseId })
            .IsUnique()
            .HasDatabaseName("uq_current_stocks_tenant_product_warehouse");
    }
}
