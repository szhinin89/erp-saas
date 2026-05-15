using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CompraNotaProveedorConfiguration : IEntityTypeConfiguration<CompraNotaProveedor>
{
    public void Configure(EntityTypeBuilder<CompraNotaProveedor> builder)
    {
        builder.ToTable("compra_notas_proveedor");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ProveedorId).HasColumnName("proveedor_id").IsRequired();
        builder.Property(x => x.CompraFacturaId).HasColumnName("compra_factura_id");
        builder.Property(x => x.GastoFacturaId).HasColumnName("gasto_factura_id");

        builder.Property(x => x.TipoNota)
            .HasColumnName("tipo_nota")
            .HasMaxLength(CompraNotaProveedor.TipoNotaMaxLen)
            .IsRequired();

        builder.Property(x => x.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(CompraNotaProveedor.MotivoMaxLen)
            .IsRequired();

        builder.Property(x => x.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(CompraNotaProveedor.ClaveAccesoMaxLen)
            .IsRequired();

        builder.Property(x => x.FechaEmision).HasColumnName("fecha_emision").IsRequired();
        builder.Property(x => x.Establecimiento)
            .HasColumnName("establecimiento")
            .HasMaxLength(CompraNotaProveedor.EstablecimientoMaxLen)
            .IsRequired();
        builder.Property(x => x.PuntoEmision)
            .HasColumnName("punto_emision")
            .HasMaxLength(CompraNotaProveedor.PuntoEmisionMaxLen)
            .IsRequired();
        builder.Property(x => x.Secuencial)
            .HasColumnName("secuencial")
            .HasMaxLength(CompraNotaProveedor.SecuencialMaxLen)
            .IsRequired();

        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Impuesto).HasColumnName("impuesto").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(x => x.Estado)
            .HasColumnName("estado")
            .HasMaxLength(CompraNotaProveedor.EstadoMaxLen)
            .IsRequired();

        builder.Property(x => x.XmlPath)
            .HasColumnName("xml_path")
            .HasMaxLength(CompraNotaProveedor.XmlPathMaxLen);

        builder.Property(x => x.NumeroAutorizacion)
            .HasColumnName("numero_autorizacion")
            .HasMaxLength(CompraNotaProveedor.NumeroAutorizacionMaxLen);

        builder.Property(x => x.FechaAutorizacion).HasColumnName("fecha_autorizacion");
        builder.Property(x => x.AsientoContableId).HasColumnName("asiento_contable_id");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.ClaveAcceso })
            .IsUnique()
            .HasDatabaseName("ix_compra_notas_proveedor_tenant_clave");

        builder.HasIndex(x => new { x.TenantId, x.ProveedorId, x.Estado })
            .HasDatabaseName("ix_compra_notas_proveedor_tenant_prov_estado");

        builder.HasIndex(x => x.CompraFacturaId)
            .HasDatabaseName("ix_compra_notas_proveedor_compra_factura");

        builder.HasIndex(x => x.GastoFacturaId)
            .HasDatabaseName("ix_compra_notas_proveedor_gasto_factura");

        builder.HasOne(x => x.Proveedor)
            .WithMany()
            .HasForeignKey(x => x.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CompraFactura)
            .WithMany()
            .HasForeignKey(x => x.CompraFacturaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GastoFactura)
            .WithMany()
            .HasForeignKey(x => x.GastoFacturaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Detalles)
            .WithOne()
            .HasForeignKey(d => d.CompraNotaProveedorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
