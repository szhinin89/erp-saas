using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AjusteInventarioConfiguration : IEntityTypeConfiguration<AjusteInventario>
{
    public void Configure(EntityTypeBuilder<AjusteInventario> builder)
    {
        builder.ToTable("ajustes_inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.Secuencial).HasColumnName("secuencial").IsRequired();

        builder.Property(e => e.NumeroAjuste)
            .HasColumnName("numero_ajuste")
            .HasMaxLength(AjusteInventario.NumeroMaxLen)
            .IsRequired();

        builder.Property(e => e.BodegaId).HasColumnName("bodega_id").IsRequired();
        builder.Property(e => e.BodegaNombre)
            .HasColumnName("bodega_nombre")
            .HasMaxLength(AjusteInventario.NombreMaxLen)
            .IsRequired();

        builder.Property(e => e.ProductoId).HasColumnName("producto_id").IsRequired();
        builder.Property(e => e.ProductoNombre)
            .HasColumnName("producto_nombre")
            .HasMaxLength(AjusteInventario.NombreMaxLen)
            .IsRequired();

        builder.Property(e => e.CantidadAjuste)
            .HasColumnName("cantidad_ajuste")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(e => e.TipoAjuste)
            .HasColumnName("tipo_ajuste")
            .HasMaxLength(AjusteInventario.TipoAjusteMaxLen)
            .IsRequired();

        builder.Property(e => e.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(AjusteInventario.MotivoMaxLen)
            .IsRequired();

        builder.Property(e => e.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(AjusteInventario.ObsMaxLen);

        builder.Property(e => e.FechaAjuste).HasColumnName("fecha_ajuste").IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(AjusteInventario.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.FechaEjecucion).HasColumnName("fecha_ejecucion");
        builder.Property(e => e.EjecutadoPor).HasColumnName("ejecutado_por");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.NumeroAjuste })
            .IsUnique()
            .HasDatabaseName("ix_ajustes_inventario_tenant_numero");

        builder.HasIndex(e => new { e.TenantId, e.Estado })
            .HasDatabaseName("ix_ajustes_inventario_tenant_estado");

        builder.HasIndex(e => new { e.TenantId, e.BodegaId })
            .HasDatabaseName("ix_ajustes_inventario_tenant_bodega");
    }
}
