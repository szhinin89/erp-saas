using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Gastos.Entities;

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

        builder.Property(g => g.ProveedorId)
            .HasColumnName("proveedor_id");

        builder.Property(g => g.NumeroFactura)
            .HasColumnName("numero_factura")
            .HasMaxLength(GastoFactura.NumeroFacturaMaxLen);

        builder.Property(g => g.FechaFactura)
            .HasColumnName("fecha_factura")
            .IsRequired();

        builder.Property(g => g.Concepto)
            .HasColumnName("concepto")
            .HasMaxLength(GastoFactura.ConceptoMaxLen)
            .IsRequired();

        builder.Property(g => g.Categoria)
            .HasColumnName("categoria")
            .HasMaxLength(GastoFactura.CategoriaMaxLen)
            .IsRequired();

        builder.Property(g => g.Monto)
            .HasColumnName("monto")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.Iva)
            .HasColumnName("iva")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.Total)
            .HasColumnName("total")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(g => g.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(GastoFactura.ObservacionesMaxLen);

        builder.Property(g => g.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        builder.Property(g => g.CreatedBy).HasColumnName("created_by");
        builder.Property(g => g.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(g => new { g.TenantId, g.FechaFactura })
            .HasDatabaseName("ix_gasto_facturas_tenant_fecha");

        builder.HasIndex(g => new { g.TenantId, g.Categoria })
            .HasDatabaseName("ix_gasto_facturas_tenant_categoria");
    }
}
