using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Infrastructure.Persistence.Configurations;

public class GastoFacturaConfiguration : IEntityTypeConfiguration<GastoFactura>
{
    public void Configure(EntityTypeBuilder<GastoFactura> builder)
    {
        builder.ToTable("gasto_facturas");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");

        builder.Property(g => g.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(g => g.ClaveAcceso)
            .HasColumnName("clave_acceso")
            .HasMaxLength(GastoFactura.ClaveAccesoLen);

        builder.Property(g => g.XmlPath)
            .HasColumnName("xml_path")
            .HasMaxLength(GastoFactura.XmlPathMaxLen);

        builder.Property(g => g.ProveedorId)
            .HasColumnName("proveedor_id");

        builder.Property(g => g.NumeroFactura)
            .HasColumnName("numero_factura")
            .HasMaxLength(GastoFactura.NumeroFacturaMaxLen);

        builder.Property(g => g.FechaEmision)
            .HasColumnName("fecha_emision")
            .IsRequired();

        builder.Property(g => g.Concepto)
            .HasColumnName("concepto")
            .HasMaxLength(GastoFactura.ConceptoMaxLen)
            .IsRequired();

        builder.Property(g => g.CategoriaGasto)
            .HasColumnName("categoria_gasto")
            .HasMaxLength(GastoFactura.CategoriaGastoMaxLen)
            .IsRequired();

        builder.Property(g => g.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.Impuesto)
            .HasColumnName("impuesto")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.Total)
            .HasColumnName("total")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.TotalNotasProveedorAplicado)
            .HasColumnName("total_notas_proveedor_aplicado")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(g => g.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(GastoFactura.ObservacionesMaxLen);

        builder.Property(g => g.ValidadoPor).HasColumnName("validado_por");
        builder.Property(g => g.ValidadoEn).HasColumnName("validado_en");
        builder.Property(g => g.AprobadoPor).HasColumnName("aprobado_por");
        builder.Property(g => g.AprobadoEn).HasColumnName("aprobado_en");
        builder.Property(g => g.RechazadoPor).HasColumnName("rechazado_por");
        builder.Property(g => g.RechazadoEn).HasColumnName("rechazado_en");
        builder.Property(g => g.MotivoRechazo)
            .HasColumnName("motivo_rechazo")
            .HasMaxLength(GastoFactura.MotivoRechazoMaxLen);
        builder.Property(g => g.AsientoContableId).HasColumnName("asiento_contable_id");

        builder.Property(g => g.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        builder.Property(g => g.CreatedBy).HasColumnName("created_by");
        builder.Property(g => g.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(g => new { g.TenantId, g.FechaEmision })
            .HasDatabaseName("ix_gasto_facturas_tenant_fecha");

        builder.HasIndex(g => new { g.TenantId, g.CategoriaGasto })
            .HasDatabaseName("ix_gasto_facturas_tenant_categoria");

        builder.HasIndex(g => new { g.TenantId, g.ClaveAcceso })
            .IsUnique()
            .HasFilter("clave_acceso IS NOT NULL")
            .HasDatabaseName("ix_gasto_facturas_tenant_clave_acceso");

        builder.HasIndex(g => new { g.TenantId, g.Estado })
            .HasDatabaseName("ix_gasto_facturas_tenant_estado");
    }
}
