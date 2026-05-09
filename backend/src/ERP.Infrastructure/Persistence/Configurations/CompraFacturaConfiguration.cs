using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Compras.Entities;
using ERP.Domain.Compras.Enums;

namespace ERP.Infrastructure.Persistence.Configurations;

public class CompraFacturaConfiguration : IEntityTypeConfiguration<CompraFactura>
{
    public void Configure(EntityTypeBuilder<CompraFactura> builder)
    {
        builder.ToTable("compra_facturas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.ProveedorId)
            .HasColumnName("proveedor_id")
            .IsRequired();

        builder.Property(c => c.NumeroFactura)
            .HasColumnName("numero_factura")
            .HasMaxLength(CompraFactura.NumeroFacturaMaxLen)
            .IsRequired();

        builder.Property(c => c.FechaFactura)
            .HasColumnName("fecha_factura")
            .IsRequired();

        builder.Property(c => c.FechaVencimiento)
            .HasColumnName("fecha_vencimiento");

        builder.Property(c => c.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CondicionPago)
            .HasColumnName("condicion_pago")
            .HasMaxLength(CompraFactura.CondicionPagoMaxLen)
            .IsRequired();

        builder.Property(c => c.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(c => c.IvaTotal)
            .HasColumnName("iva_total")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(c => c.Total)
            .HasColumnName("total")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(c => c.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(CompraFactura.ObservacionesMaxLen);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        // Número de factura único por proveedor y tenant
        builder.HasIndex(c => new { c.TenantId, c.ProveedorId, c.NumeroFactura })
            .IsUnique()
            .HasDatabaseName("ix_compra_facturas_tenant_proveedor_numero");

        builder.HasIndex(c => new { c.TenantId, c.Estado })
            .HasDatabaseName("ix_compra_facturas_tenant_estado");

        builder.HasMany(c => c.Detalles)
            .WithOne()
            .HasForeignKey(d => d.CompraFacturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
