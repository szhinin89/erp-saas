using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouse");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(w => w.BranchId).HasColumnName("establishment_id").IsRequired();
        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(Warehouse.NameMaxLen).IsRequired();
        builder.Property(w => w.Address).HasColumnName("address").HasMaxLength(Warehouse.AddressMaxLen);
        builder.Property(w => w.Manager).HasColumnName("manager").HasMaxLength(Warehouse.ManagerMaxLen);
        builder.Property(w => w.EstablishmentId).HasColumnName("sri_establishment_id");
        builder.Property(w => w.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(w => new { w.TenantId, w.Name }).IsUnique().HasDatabaseName("uq_warehouse_tenant_name");
        builder.HasIndex(w => w.BranchId).HasDatabaseName("ix_warehouse_establishment_id");
    }
}
