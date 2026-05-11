using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Configuration.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionRetencionConfiguration : IEntityTypeConfiguration<ConfiguracionRetencion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionRetencion> builder)
    {
        builder.ToTable("configuracion_retenciones");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.Impuesto)
            .HasColumnName("impuesto")
            .HasMaxLength(ConfiguracionRetencion.ImpuestoMaxLen)
            .IsRequired();

        builder.Property(e => e.TipoSujeto)
            .HasColumnName("tipo_sujeto")
            .HasMaxLength(ConfiguracionRetencion.TipoSujetoMaxLen)
            .IsRequired();

        builder.Property(e => e.CodigoSri)
            .HasColumnName("codigo_sri")
            .HasMaxLength(ConfiguracionRetencion.CodigoSriMaxLen)
            .IsRequired();

        builder.Property(e => e.Porcentaje).HasColumnName("porcentaje").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Activo).HasColumnName("activo").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.Impuesto, e.TipoSujeto, e.CodigoSri })
            .IsUnique()
            .HasDatabaseName("ix_config_retenciones_tenant_impuesto_sujeto_codigo");
    }
}
