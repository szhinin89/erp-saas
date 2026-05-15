using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class BodegaConfiguration : IEntityTypeConfiguration<Bodega>
{
    public void Configure(EntityTypeBuilder<Bodega> builder)
    {
        // Tabla renombrada de 'bodegas' → 'warehouse' (sin datos reales)
        builder.ToTable("warehouse");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();

        // SucursalId mapea a 'establishment_id' (mismo concepto, renombrado)
        builder.Property(b => b.SucursalId)
            .HasColumnName("establishment_id")
            .IsRequired();
        builder.Property(b => b.Nombre)
            .HasColumnName("name")
            .HasMaxLength(Bodega.NombreMaxLen)
            .IsRequired();
        builder.Property(b => b.Ubicacion)
            .HasColumnName("address")
            .HasMaxLength(Bodega.UbicacionMaxLen);
        builder.Property(b => b.Encargado)
            .HasColumnName("manager")
            .HasMaxLength(Bodega.EncargadoMaxLen);

        // ── Campo SRI ────────────────────────────────────────────
        builder.Property(b => b.EstablishmentId)
            .HasColumnName("sri_establishment_id");

        builder.Property(b => b.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(b => new { b.TenantId, b.Nombre })
            .IsUnique()
            .HasDatabaseName("uq_warehouse_tenant_name");
        builder.HasIndex(b => b.SucursalId)
            .HasDatabaseName("ix_warehouse_establishment_id");
    }
}
