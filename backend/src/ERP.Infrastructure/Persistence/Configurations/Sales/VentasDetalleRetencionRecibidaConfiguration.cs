using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class VentasDetalleRetencionRecibidaConfiguration : IEntityTypeConfiguration<VentasDetalleRetencionRecibida>
{
    public void Configure(EntityTypeBuilder<VentasDetalleRetencionRecibida> builder)
    {
        builder.ToTable("ventas_detalle_retenciones_recibidas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.VentasRetencionRecibidaId).HasColumnName("ventas_retencion_recibida_id").IsRequired();

        builder.Property(e => e.Impuesto)
            .HasColumnName("impuesto")
            .HasMaxLength(VentasDetalleRetencionRecibida.ImpuestoMaxLen)
            .IsRequired();

        builder.Property(e => e.CodigoRetencion)
            .HasColumnName("codigo_retencion")
            .HasMaxLength(VentasDetalleRetencionRecibida.CodigoRetencionMaxLen)
            .IsRequired();

        builder.Property(e => e.BaseImponible).HasColumnName("base_imponible").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.PorcentajeRetencion).HasColumnName("porcentaje_retencion").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.ValorRetenido).HasColumnName("valor_retenido").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}
