using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Inventario.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class InventarioMovimientoConfiguration : IEntityTypeConfiguration<InventarioMovimiento>
{
    public void Configure(EntityTypeBuilder<InventarioMovimiento> builder)
    {
        builder.ToTable("inventario_movimientos");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.ProductoId)
            .HasColumnName("producto_id")
            .IsRequired();

        builder.Property(m => m.BodegaId)
            .HasColumnName("bodega_id")
            .IsRequired();

        builder.Property(m => m.TipoMovimiento)
            .HasColumnName("tipo_movimiento")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(m => m.Cantidad)
            .HasColumnName("cantidad")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(m => m.CantidadAnterior)
            .HasColumnName("cantidad_anterior")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(m => m.CantidadResultante)
            .HasColumnName("cantidad_resultante")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(m => m.Referencia)
            .HasColumnName("referencia")
            .HasMaxLength(InventarioMovimiento.ReferenciaMaxLen);

        builder.Property(m => m.DocumentoOrigenId)
            .HasColumnName("documento_origen_id");

        builder.Property(m => m.DocumentoOrigenTipo)
            .HasColumnName("documento_origen_tipo")
            .HasMaxLength(InventarioMovimiento.DocumentoOrigenTipoMaxLen);

        builder.Property(m => m.CostoUnitario)
            .HasColumnName("costo_unitario")
            .HasPrecision(18, 6);

        builder.Property(m => m.CostoTotal)
            .HasColumnName("costo_total")
            .HasPrecision(18, 6);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(m => new { m.TenantId, m.ProductoId, m.BodegaId })
            .HasDatabaseName("ix_inventario_movimientos_tenant_producto_bodega");

        builder.HasIndex(m => new { m.TenantId, m.TipoMovimiento })
            .HasDatabaseName("ix_inventario_movimientos_tenant_tipo");

        builder.HasIndex(m => m.DocumentoOrigenId)
            .HasDatabaseName("ix_inventario_movimientos_documento_origen");
    }
}
