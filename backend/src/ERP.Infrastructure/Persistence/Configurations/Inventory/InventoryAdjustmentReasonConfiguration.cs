using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class InventoryAdjustmentReasonConfiguration
    : IEntityTypeConfiguration<InventoryAdjustmentReason>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustmentReason> builder)
    {
        builder.ToTable("inventory_adjustment_reasons");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(InventoryAdjustmentReason.CodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(InventoryAdjustmentReason.NameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.AllowedMovementType)
            .HasColumnName("allowed_movement_type")
            .HasMaxLength(InventoryAdjustmentReason.MovementTypeMaxLen)
            .IsRequired();
        builder.Property(x => x.RequiresNotes).HasColumnName("requires_notes").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_inventory_adjustment_reasons_tenant_code");
    }
}
