using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class VentasNotaCreditoDebitoConfiguration : IEntityTypeConfiguration<VentasNotaCreditoDebito>
{
    public void Configure(EntityTypeBuilder<VentasNotaCreditoDebito> builder)
    {
        builder.ToTable("ventas_notas_credito_debito");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.VentasFacturaOriginalId).HasColumnName("ventas_factura_original_id").IsRequired();

        builder.Property(e => e.TipoNota)
            .HasColumnName("tipo_nota")
            .HasMaxLength(VentasNotaCreditoDebito.TipoNotaMaxLen)
            .IsRequired();

        builder.Property(e => e.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(VentasNotaCreditoDebito.MotivoMaxLen)
            .IsRequired();

        builder.Property(e => e.TipoDocumento)
            .HasColumnName("tipo_documento")
            .HasMaxLength(VentasNotaCreditoDebito.TipoDocumentoMaxLen)
            .IsRequired();

        builder.Property(e => e.Establecimiento)
            .HasColumnName("establecimiento")
            .HasMaxLength(VentasNotaCreditoDebito.EstablecimientoMaxLen)
            .IsRequired();

        builder.Property(e => e.PuntoEmision)
            .HasColumnName("punto_emision")
            .HasMaxLength(VentasNotaCreditoDebito.PuntoEmisionMaxLen)
            .IsRequired();

        builder.Property(e => e.Secuencial)
            .HasColumnName("secuencial")
            .HasMaxLength(VentasNotaCreditoDebito.SecuencialMaxLen)
            .IsRequired();

        builder.Property(e => e.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(VentasNotaCreditoDebito.ClaveAccesoMaxLen)
            .IsRequired();

        builder.Property(e => e.FechaEmision).HasColumnName("fecha_emision").IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Impuesto).HasColumnName("impuesto").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(VentasNotaCreditoDebito.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.XmlGeneradoPath).HasColumnName("xml_generado_path").HasMaxLength(VentasNotaCreditoDebito.XmlPathMaxLen);
        builder.Property(e => e.XmlAutorizacionPath).HasColumnName("xml_autorizacion_path").HasMaxLength(VentasNotaCreditoDebito.XmlPathMaxLen);
        builder.Property(e => e.NumeroAutorizacion).HasColumnName("numero_autorizacion").HasMaxLength(64);
        builder.Property(e => e.FechaAutorizacion).HasColumnName("fecha_autorizacion");
        builder.Property(e => e.MensajeError).HasColumnName("mensaje_error").HasMaxLength(VentasNotaCreditoDebito.MensajeErrorMaxLen);
        builder.Property(e => e.AsientoContableId).HasColumnName("asiento_contable_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.FacturaOriginal)
            .WithMany()
            .HasForeignKey(e => e.VentasFacturaOriginalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Detalles)
            .WithOne()
            .HasForeignKey(d => d.VentasNotaCreditoDebitoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.Establecimiento, e.PuntoEmision, e.Secuencial })
            .IsUnique()
            .HasDatabaseName("ix_ventas_notas_tenant_estab_punto_secuencial");

        builder.HasIndex(e => new { e.TenantId, e.VentasFacturaOriginalId })
            .HasDatabaseName("ix_ventas_notas_tenant_factura_origen");
    }
}
