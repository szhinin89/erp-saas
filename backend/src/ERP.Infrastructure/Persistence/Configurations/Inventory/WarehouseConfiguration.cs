using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(Warehouse.NameMaxLen).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(Warehouse.CodeMaxLen);
        builder.Property(x => x.StorageType).HasColumnName("storage_type")
            .HasMaxLength(Warehouse.StorageTypeMaxLen);
        builder.Property(x => x.Address).HasColumnName("address")
            .HasMaxLength(Warehouse.AddressMaxLen);
        builder.Property(x => x.Phone).HasColumnName("phone")
            .HasMaxLength(Warehouse.PhoneMaxLen);
        builder.Property(x => x.Email).HasColumnName("email")
            .HasMaxLength(Warehouse.EmailMaxLen);
        builder.Property(x => x.Manager).HasColumnName("manager")
            .HasMaxLength(Warehouse.ManagerMaxLen);
        builder.Property(x => x.Latitude).HasColumnName("latitude")
            .HasMaxLength(Warehouse.LatLonMaxLen);
        builder.Property(x => x.Longitude).HasColumnName("longitude")
            .HasMaxLength(Warehouse.LatLonMaxLen);
        builder.Property(x => x.Capacity).HasColumnName("capacity")
            .HasColumnType("numeric(18,4)");
        builder.Property(x => x.DailyDispatchGoal).HasColumnName("daily_dispatch_goal")
            .HasColumnType("numeric(18,4)");
        builder.Property(x => x.IsMain).HasColumnName("is_main").IsRequired();
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_warehouses_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_warehouses_tenant_branch_code")
            .HasFilter("code IS NOT NULL");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Establishment>()
            .WithMany()
            .HasForeignKey(x => x.EstablishmentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
