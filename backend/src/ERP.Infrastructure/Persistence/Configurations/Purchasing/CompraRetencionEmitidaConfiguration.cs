using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CompraRetencionEmitidaConfiguration : IEntityTypeConfiguration<CompraRetencionEmitida>
{
    public void Configure(EntityTypeBuilder<CompraRetencionEmitida> builder)
    {
        builder.ToTable("compra_retenciones_emitidas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.ProveedorId).HasColumnName("proveedor_id").IsRequired();
        builder.Property(e => e.CompraFacturaId).HasColumnName("compra_factura_id");

        builder.Property(e => e.TipoComprobante)
            .HasColumnName("tipo_comprobante")
            .HasMaxLength(CompraRetencionEmitida.TipoComprobanteMaxLen)
            .IsRequired();

        builder.Property(e => e.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(CompraRetencionEmitida.ClaveAccesoMaxLen)
            .IsRequired();

        builder.Property(e => e.FechaEmision).HasColumnName("fecha_emision").IsRequired();

        builder.Property(e => e.Establecimiento)
            .HasColumnName("establecimiento")
            .HasMaxLength(CompraRetencionEmitida.EstablecimientoMaxLen)
            .IsRequired();

        builder.Property(e => e.PuntoEmision)
            .HasColumnName("punto_emision")
            .HasMaxLength(CompraRetencionEmitida.PuntoEmisionMaxLen)
            .IsRequired();

        builder.Property(e => e.Secuencial)
            .HasColumnName("secuencial")
            .HasMaxLength(CompraRetencionEmitida.SecuencialMaxLen)
            .IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(CompraRetencionEmitida.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.TotalRetenido).HasColumnName("total_retenido").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.XmlGeneradoPath).HasColumnName("xml_generado_path").HasMaxLength(CompraRetencionEmitida.XmlPathMaxLen);
        builder.Property(e => e.XmlAutorizacionPath).HasColumnName("xml_autorizacion_path").HasMaxLength(CompraRetencionEmitida.XmlPathMaxLen);
        builder.Property(e => e.NumeroAutorizacion).HasColumnName("numero_autorizacion").HasMaxLength(64);
        builder.Property(e => e.FechaAutorizacion).HasColumnName("fecha_autorizacion");
        builder.Property(e => e.MensajeError).HasColumnName("mensaje_error").HasMaxLength(CompraRetencionEmitida.MensajeErrorMaxLen);
        builder.Property(e => e.AsientoContableId).HasColumnName("asiento_contable_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Proveedor)
            .WithMany()
            .HasForeignKey(e => e.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CompraFactura>()
            .WithMany()
            .HasForeignKey(e => e.CompraFacturaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Detalles)
            .WithOne()
            .HasForeignKey(d => d.CompraRetencionEmitidaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Establecimiento, e.PuntoEmision, e.Secuencial })
            .IsUnique()
            .HasDatabaseName("ix_compra_retenciones_emitidas_tenant_estab_punto_seq");
    }
}
