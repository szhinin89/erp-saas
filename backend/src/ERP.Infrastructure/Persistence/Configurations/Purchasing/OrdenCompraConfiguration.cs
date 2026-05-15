using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class OrdenCompraConfiguration : IEntityTypeConfiguration<OrdenCompra>
{
    public void Configure(EntityTypeBuilder<OrdenCompra> builder)
    {
        builder.ToTable("ordenes_compra");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.Secuencial).HasColumnName("secuencial").IsRequired();

        builder.Property(e => e.NumeroOrden)
            .HasColumnName("numero_orden")
            .HasMaxLength(OrdenCompra.NumeroMaxLen)
            .IsRequired();

        builder.Property(e => e.ProveedorId).HasColumnName("proveedor_id").IsRequired();

        builder.Property(e => e.FechaEmision).HasColumnName("fecha_emision").IsRequired();
        builder.Property(e => e.FechaRequerida).HasColumnName("fecha_requerida").IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(OrdenCompra.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.Moneda)
            .HasColumnName("moneda")
            .HasMaxLength(OrdenCompra.MonedaMaxLen)
            .IsRequired();

        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Impuesto).HasColumnName("impuesto").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(OrdenCompra.ObsMaxLen);

        builder.Property(e => e.DireccionEntrega)
            .HasColumnName("direccion_entrega")
            .HasMaxLength(OrdenCompra.DireccionMaxLen);

        builder.Property(e => e.BodegaDestinoId).HasColumnName("bodega_destino_id");

        builder.Property(e => e.FechaEnvio).HasColumnName("fecha_envio");
        builder.Property(e => e.FechaAprobacion).HasColumnName("fecha_aprobacion");
        builder.Property(e => e.AprobadoPor).HasColumnName("aprobado_por");
        builder.Property(e => e.FechaCierre).HasColumnName("fecha_cierre");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(e => e.Detalles)
            .WithOne()
            .HasForeignKey(d => d.OrdenCompraId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.NumeroOrden })
            .IsUnique()
            .HasDatabaseName("ix_ordenes_compra_tenant_numero");

        builder.HasIndex(e => new { e.TenantId, e.Estado })
            .HasDatabaseName("ix_ordenes_compra_tenant_estado");

        builder.HasIndex(e => new { e.TenantId, e.ProveedorId })
            .HasDatabaseName("ix_ordenes_compra_tenant_proveedor");
    }
}
