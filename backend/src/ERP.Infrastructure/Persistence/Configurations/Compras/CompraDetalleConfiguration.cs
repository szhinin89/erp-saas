using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Compras.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class CompraDetalleConfiguration : IEntityTypeConfiguration<CompraDetalle>
{
    public void Configure(EntityTypeBuilder<CompraDetalle> builder)
    {
        builder.ToTable("compra_detalles");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.CompraFacturaId).HasColumnName("compra_factura_id").IsRequired();
        builder.Property(d => d.ProductoId).HasColumnName("producto_id");   // nullable
        builder.Property(d => d.OrdenCompraDetalleId).HasColumnName("orden_compra_detalle_id"); // nullable

        builder.Property(d => d.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(CompraDetalle.DescripcionMaxLen)
            .IsRequired();

        builder.Property(d => d.CodigoPrincipalProveedor)
            .HasColumnName("codigo_principal_proveedor")
            .HasMaxLength(CompraDetalle.CodigoPrincipalMaxLen);

        builder.Property(d => d.Cantidad).HasColumnName("cantidad").HasPrecision(18, 6).IsRequired();
        builder.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.DescuentoPorcentaje).HasColumnName("descuento_porcentaje").HasPrecision(9, 4).IsRequired();
        builder.Property(d => d.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.IvaPorcentaje).HasColumnName("iva_porcentaje").HasPrecision(9, 4).IsRequired();
        builder.Property(d => d.IvaValor).HasColumnName("iva_valor").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(d => d.CompraFacturaId).HasDatabaseName("ix_compra_detalles_compra_factura_id");
        builder.HasIndex(d => new { d.TenantId, d.ProductoId })
            .HasDatabaseName("ix_compra_detalles_tenant_producto");
    }
}
