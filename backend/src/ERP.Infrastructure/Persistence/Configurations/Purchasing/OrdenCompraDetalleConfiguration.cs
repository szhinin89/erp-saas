using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class OrdenCompraDetalleConfiguration : IEntityTypeConfiguration<OrdenCompraDetalle>
{
    public void Configure(EntityTypeBuilder<OrdenCompraDetalle> builder)
    {
        builder.ToTable("ordenes_compra_detalles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.OrdenCompraId).HasColumnName("orden_compra_id").IsRequired();
        builder.Property(e => e.ProductoId).HasColumnName("producto_id").IsRequired();

        builder.Property(e => e.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(OrdenCompraDetalle.DescripcionMaxLen)
            .IsRequired();

        builder.Property(e => e.CantidadPedida)
            .HasColumnName("cantidad_pedida")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(e => e.CantidadFacturada)
            .HasColumnName("cantidad_facturada")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(e => e.PrecioUnitario)
            .HasColumnName("precio_unitario")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.Impuesto)
            .HasColumnName("impuesto")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.Total)
            .HasColumnName("total")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Ignore(e => e.PendienteFacturar);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.OrdenCompraId })
            .HasDatabaseName("ix_ordenes_compra_detalles_tenant_orden");
    }
}
