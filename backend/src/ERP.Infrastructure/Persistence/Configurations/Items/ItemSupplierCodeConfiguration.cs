using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Items.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemSupplierCodeConfiguration : IEntityTypeConfiguration<ItemSupplierCode>
{
    public void Configure(EntityTypeBuilder<ItemSupplierCode> builder)
    {
        builder.ToTable("item_supplier_codes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un código de proveedor identifica un único ítem en todo el catálogo del tenant
        // (Fase 2) — ya no está acotado por item_id.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SupplierId,
                x.Code,
            })
            .IsUnique()
            .HasDatabaseName("uq_item_supplier_codes_tenant_supplier_code");

        // A lo sumo un código de proveedor marcado como principal por ítem (no obligatorio).
        builder
            .HasIndex(x => new { x.ItemId, x.IsPrimary })
            .HasFilter("is_primary = true")
            .IsUnique()
            .HasDatabaseName("uq_item_supplier_codes_primary");

        // Lookup general: todos los códigos de proveedor activos de un ítem.
        builder
            .HasIndex(x => new { x.ItemId, x.IsActive })
            .HasDatabaseName("ix_item_supplier_codes_item_active");
    }
}
