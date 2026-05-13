using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Compras.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class CompraBodegaAsignacionConfiguration : IEntityTypeConfiguration<CompraBodegaAsignacion>
{
    public void Configure(EntityTypeBuilder<CompraBodegaAsignacion> builder)
    {
        builder.ToTable("compra_bodega_asignaciones");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(a => a.CompraFacturaId)
            .HasColumnName("compra_factura_id")
            .IsRequired();

        builder.Property(a => a.CompraDetalleId)
            .HasColumnName("compra_detalle_id")
            .IsRequired();

        builder.Property(a => a.BodegaId)
            .HasColumnName("bodega_id")
            .IsRequired();

        builder.Property(a => a.ProductoId)
            .HasColumnName("producto_id");

        builder.Property(a => a.Cantidad)
            .HasColumnName("cantidad")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(a => a.CompraDetalleId)
            .HasDatabaseName("ix_compra_bodega_asignaciones_detalle_id");

        builder.HasIndex(a => new { a.TenantId, a.BodegaId })
            .HasDatabaseName("ix_compra_bodega_asignaciones_tenant_bodega");
    }
}
