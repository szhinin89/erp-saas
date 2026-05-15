using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class VentasRetencionRecibidaConfiguration : IEntityTypeConfiguration<VentasRetencionRecibida>
{
    public void Configure(EntityTypeBuilder<VentasRetencionRecibida> builder)
    {
        builder.ToTable("ventas_retenciones_recibidas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.ClienteId).HasColumnName("cliente_id").IsRequired();

        builder.Property(e => e.TipoComprobante)
            .HasColumnName("tipo_comprobante")
            .HasMaxLength(VentasRetencionRecibida.TipoComprobanteMaxLen)
            .IsRequired();

        builder.Property(e => e.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(VentasRetencionRecibida.ClaveAccesoMaxLen)
            .IsRequired();

        builder.Property(e => e.FechaEmision).HasColumnName("fecha_emision").IsRequired();
        builder.Property(e => e.ValorRetenido).HasColumnName("valor_retenido").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(VentasRetencionRecibida.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.VentasFacturaId).HasColumnName("ventas_factura_id");
        builder.Property(e => e.XmlRegistroPath).HasColumnName("xml_registro_path").HasMaxLength(VentasRetencionRecibida.XmlPathMaxLen);
        builder.Property(e => e.AsientoContableId).HasColumnName("asiento_contable_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Cliente)
            .WithMany()
            .HasForeignKey(e => e.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VentasFactura>()
            .WithMany()
            .HasForeignKey(e => e.VentasFacturaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Detalles)
            .WithOne()
            .HasForeignKey(d => d.VentasRetencionRecibidaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.ClaveAcceso }).IsUnique()
            .HasDatabaseName("ix_ventas_retenciones_recibidas_tenant_clave");
    }
}
