using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Compras.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class CompraFacturaConfiguration : IEntityTypeConfiguration<CompraFactura>
{
    public void Configure(EntityTypeBuilder<CompraFactura> builder)
    {
        builder.ToTable("compra_facturas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.ProveedorId).HasColumnName("proveedor_id").IsRequired();

        builder.Property(c => c.NumeroFactura)
            .HasColumnName("numero_factura")
            .HasMaxLength(CompraFactura.NumeroFacturaMaxLen)
            .IsRequired();

        builder.Property(c => c.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(CompraFactura.ClaveAccesoLen);

        builder.Property(c => c.XmlPath)
            .HasColumnName("xml_path")
            .HasMaxLength(CompraFactura.XmlPathMaxLen);

        builder.Property(c => c.FechaFactura).HasColumnName("fecha_factura").IsRequired();
        builder.Property(c => c.FechaVencimiento).HasColumnName("fecha_vencimiento");

        builder.Property(c => c.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CondicionPago)
            .HasColumnName("condicion_pago")
            .HasMaxLength(CompraFactura.CondicionPagoMaxLen)
            .IsRequired();

        builder.Property(c => c.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.IvaTotal).HasColumnName("iva_total").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(c => c.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(CompraFactura.ObservacionesMaxLen);

        // ── Auditoría de estado ───────────────────────────────────────────
        builder.Property(c => c.ValidadoPor).HasColumnName("validado_por");
        builder.Property(c => c.ValidadoEn).HasColumnName("validado_en");
        builder.Property(c => c.AprobadoPor).HasColumnName("aprobado_por");
        builder.Property(c => c.AprobadoEn).HasColumnName("aprobado_en");
        builder.Property(c => c.RechazadoPor).HasColumnName("rechazado_por");
        builder.Property(c => c.RechazadoEn).HasColumnName("rechazado_en");
        builder.Property(c => c.MotivoRechazo)
            .HasColumnName("motivo_rechazo")
            .HasMaxLength(CompraFactura.MotivoRechazoMaxLen);
        builder.Property(c => c.AsientoContableId).HasColumnName("asiento_contable_id");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(c => new { c.TenantId, c.ProveedorId, c.NumeroFactura })
            .IsUnique()
            .HasDatabaseName("ix_compra_facturas_tenant_proveedor_numero");

        builder.HasIndex(c => new { c.TenantId, c.ClaveAcceso })
            .IsUnique()
            .HasFilter("clave_acceso IS NOT NULL")
            .HasDatabaseName("ix_compra_facturas_tenant_clave_acceso");

        builder.HasIndex(c => new { c.TenantId, c.Estado })
            .HasDatabaseName("ix_compra_facturas_tenant_estado");

        // Listados por proveedor y estado (reportes / bandejas).
        builder.HasIndex(c => new { c.TenantId, c.ProveedorId, c.Estado })
            .HasDatabaseName("ix_compra_facturas_tenant_proveedor_estado");

        builder.HasMany(c => c.Detalles)
            .WithOne()
            .HasForeignKey(d => d.CompraFacturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
