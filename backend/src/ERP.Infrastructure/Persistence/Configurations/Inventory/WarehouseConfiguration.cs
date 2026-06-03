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
        builder.Property(w => w.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(w => w.CompanyId).HasColumnName("company_id");
        builder.HasIndex(w => new { w.SubscriberId, w.CompanyId }).HasDatabaseName("ix_warehouse_subscriber_company");
        builder.Property(w => w.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(Warehouse.NameMaxLen).IsRequired();
        builder.Property(w => w.Code).HasColumnName("code").HasMaxLength(Warehouse.CodeMaxLen);
        builder.Property(w => w.StorageType).HasColumnName("storage_type").HasMaxLength(Warehouse.StorageTypeMaxLen);
        builder.Property(w => w.Address).HasColumnName("address").HasMaxLength(Warehouse.AddressMaxLen);
        builder.Property(w => w.Phone).HasColumnName("phone").HasMaxLength(Warehouse.PhoneMaxLen);
        builder.Property(w => w.Email).HasColumnName("email").HasMaxLength(Warehouse.EmailMaxLen);
        builder.Property(w => w.Manager).HasColumnName("manager").HasMaxLength(Warehouse.ManagerMaxLen);
        builder.Property(w => w.Latitude).HasColumnName("latitude").HasMaxLength(Warehouse.LatLonMaxLen);
        builder.Property(w => w.Longitude).HasColumnName("longitude").HasMaxLength(Warehouse.LatLonMaxLen);
        builder.Property(w => w.Capacity).HasColumnName("capacity").HasColumnType("decimal(12,2)");
        builder.Property(w => w.DailyDispatchGoal).HasColumnName("daily_dispatch_goal").HasColumnType("decimal(12,2)");
        builder.Property(w => w.EstablishmentId).HasColumnName("sri_establishment_id");
        builder.Property(w => w.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(w => new { w.SubscriberId, w.Name }).IsUnique().HasDatabaseName("uq_warehouse_subscriber_name");
        builder.HasIndex(w => w.BranchId).HasDatabaseName("ix_warehouse_branch_id");
    }
}
