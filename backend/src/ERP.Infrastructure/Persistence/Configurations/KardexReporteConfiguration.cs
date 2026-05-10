using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Inventario.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class KardexReporteConfiguration : IEntityTypeConfiguration<KardexReporte>
{
    public void Configure(EntityTypeBuilder<KardexReporte> builder)
    {
        builder.ToTable("kardex_reportes");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id").IsRequired();

        builder.Property(r => r.ProductoId)
            .HasColumnName("producto_id").IsRequired();

        builder.Property(r => r.BodegaId)
            .HasColumnName("bodega_id").IsRequired();

        builder.Property(r => r.FechaInicio)
            .HasColumnName("fecha_inicio");

        builder.Property(r => r.FechaFin)
            .HasColumnName("fecha_fin");

        builder.Property(r => r.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20).IsRequired();

        builder.Property(r => r.ResultadoJson)
            .HasColumnName("resultado_json")
            .HasColumnType("text");

        builder.Property(r => r.ErrorMensaje)
            .HasColumnName("error_mensaje")
            .HasMaxLength(1000);

        builder.Property(r => r.SolicitadoEn)
            .HasColumnName("solicitado_en").IsRequired();

        builder.Property(r => r.CompletadoEn)
            .HasColumnName("completado_en");

        builder.HasIndex(r => new { r.TenantId, r.Estado })
            .HasDatabaseName("ix_kardex_reportes_tenant_estado");

        builder.HasIndex(r => r.SolicitadoEn)
            .HasDatabaseName("ix_kardex_reportes_solicitado_en");
    }
}
