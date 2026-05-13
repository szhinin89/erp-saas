using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Ventas.Entities;
using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class VentasFacturaConfiguration : IEntityTypeConfiguration<VentasFactura>
{
    public void Configure(EntityTypeBuilder<VentasFactura> builder)
    {
        builder.ToTable("ventas_facturas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SucursalId).HasColumnName("sucursal_id").IsRequired();
        builder.Property(e => e.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(e => e.BodegaId).HasColumnName("bodega_id").IsRequired();

        builder.Property(e => e.TipoDocumento)
            .HasColumnName("tipo_documento")
            .HasMaxLength(VentasFactura.TipoDocumentoMaxLen)
            .IsRequired();

        builder.Property(e => e.Establecimiento)
            .HasColumnName("establecimiento")
            .HasMaxLength(VentasFactura.EstablecimientoMaxLen)
            .IsRequired();

        builder.Property(e => e.PuntoEmision)
            .HasColumnName("punto_emision")
            .HasMaxLength(VentasFactura.PuntoEmisionMaxLen)
            .IsRequired();

        builder.Property(e => e.Secuencial)
            .HasColumnName("secuencial")
            .HasMaxLength(VentasFactura.SecuencialMaxLen)
            .IsRequired();

        builder.Property(e => e.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(VentasFactura.ClaveAccesoMaxLen)
            .IsRequired();

        builder.Property(e => e.FechaEmision)
            .HasColumnName("fecha_emision")
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

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(VentasFactura.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.XmlGeneradoPath)
            .HasColumnName("xml_generado_path")
            .HasMaxLength(VentasFactura.XmlPathMaxLen);

        builder.Property(e => e.XmlAutorizacionPath)
            .HasColumnName("xml_autorizacion_path")
            .HasMaxLength(VentasFactura.XmlPathMaxLen);

        builder.Property(e => e.NumeroAutorizacion)
            .HasColumnName("numero_autorizacion")
            .HasMaxLength(VentasFactura.ClaveAccesoMaxLen);

        builder.Property(e => e.FechaAutorizacion)
            .HasColumnName("fecha_autorizacion");

        builder.Property(e => e.MensajeError)
            .HasColumnName("mensaje_error")
            .HasMaxLength(VentasFactura.MensajeErrorMaxLen);

        builder.Property(e => e.AsientoContableId)
            .HasColumnName("asiento_contable_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Cliente)
            .WithMany()
            .HasForeignKey(e => e.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Bodega)
            .WithMany()
            .HasForeignKey(e => e.BodegaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Detalles)
            .WithOne()
            .HasForeignKey(d => d.VentasFacturaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Establecimiento, e.PuntoEmision, e.Secuencial })
            .IsUnique()
            .HasDatabaseName("ix_ventas_facturas_tenant_estab_punto_secuencial");

        // Reportes y consultas por rango de fechas (multi-tenant).
        builder.HasIndex(e => new { e.TenantId, e.FechaEmision })
            .HasDatabaseName("ix_ventas_facturas_tenant_fecha_emision");
    }
}
