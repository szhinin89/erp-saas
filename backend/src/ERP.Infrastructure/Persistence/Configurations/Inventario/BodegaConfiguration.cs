using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Inventario.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class BodegaConfiguration : IEntityTypeConfiguration<Bodega>
{
    public void Configure(EntityTypeBuilder<Bodega> builder)
    {
        builder.ToTable("bodegas");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");

        builder.Property(b => b.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(b => b.SucursalId)
            .HasColumnName("sucursal_id")
            .IsRequired();

        builder.Property(b => b.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(Bodega.NombreMaxLen)
            .IsRequired();

        builder.Property(b => b.Ubicacion)
            .HasColumnName("ubicacion")
            .HasMaxLength(Bodega.UbicacionMaxLen);

        builder.Property(b => b.Encargado)
            .HasColumnName("encargado")
            .HasMaxLength(Bodega.EncargadoMaxLen);

        builder.Property(b => b.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(b => new { b.TenantId, b.Nombre })
            .IsUnique()
            .HasDatabaseName("ix_bodegas_tenant_nombre");

        builder.HasIndex(b => b.SucursalId)
            .HasDatabaseName("ix_bodegas_sucursal_id");
    }
}
