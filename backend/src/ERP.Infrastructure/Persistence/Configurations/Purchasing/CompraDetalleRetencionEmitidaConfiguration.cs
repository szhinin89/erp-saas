using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CompraDetalleRetencionEmitidaConfiguration : IEntityTypeConfiguration<CompraDetalleRetencionEmitida>
{
    public void Configure(EntityTypeBuilder<CompraDetalleRetencionEmitida> builder)
    {
        builder.ToTable("compra_detalle_retenciones_emitidas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompraRetencionEmitidaId).HasColumnName("compra_retencion_emitida_id").IsRequired();

        builder.Property(e => e.Impuesto)
            .HasColumnName("impuesto")
            .HasMaxLength(CompraDetalleRetencionEmitida.ImpuestoMaxLen)
            .IsRequired();

        builder.Property(e => e.CodigoRetencion)
            .HasColumnName("codigo_retencion")
            .HasMaxLength(CompraDetalleRetencionEmitida.CodigoRetencionMaxLen)
            .IsRequired();

        builder.Property(e => e.BaseImponible).HasColumnName("base_imponible").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.PorcentajeRetencion).HasColumnName("porcentaje_retencion").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.ValorRetenido).HasColumnName("valor_retenido").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.FacturaRelacionada)
            .HasColumnName("factura_relacionada")
            .HasMaxLength(CompraDetalleRetencionEmitida.FacturaRelacionadaMaxLen);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}
