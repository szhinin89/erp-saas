using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Configuration.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ConfiguracionSRIConfiguration : IEntityTypeConfiguration<ConfiguracionSRI>
{
    public void Configure(EntityTypeBuilder<ConfiguracionSRI> builder)
    {
        builder.ToTable("configuracion_sri");

        builder.HasKey(e => e.TenantId);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.RucEmpresa)
            .HasColumnName("ruc_empresa")
            .HasMaxLength(ConfiguracionSRI.RucEmpresaMaxLen)
            .IsRequired();

        builder.Property(e => e.RazonSocial)
            .HasColumnName("razon_social")
            .HasMaxLength(ConfiguracionSRI.RazonSocialMaxLen)
            .IsRequired();

        builder.Property(e => e.NombreComercial)
            .HasColumnName("nombre_comercial")
            .HasMaxLength(ConfiguracionSRI.NombreComercialMaxLen);

        builder.Property(e => e.DireccionMatriz)
            .HasColumnName("direccion_matriz")
            .HasMaxLength(ConfiguracionSRI.DireccionMatrizMaxLen)
            .IsRequired();

        builder.Property(e => e.ObligadoContabilidad)
            .HasColumnName("obligado_contabilidad")
            .IsRequired();

        builder.Property(e => e.ContribuyenteEspecial)
            .HasColumnName("contribuyente_especial")
            .HasMaxLength(ConfiguracionSRI.ContribuyenteEspecialMaxLen);

        builder.Property(e => e.Establecimiento)
            .HasColumnName("establecimiento")
            .HasMaxLength(ConfiguracionSRI.EstablecimientoMaxLen)
            .IsRequired();

        builder.Property(e => e.PuntoEmision)
            .HasColumnName("punto_emision")
            .HasMaxLength(ConfiguracionSRI.PuntoEmisionMaxLen)
            .IsRequired();

        builder.Property(e => e.SecuencialActual)
            .HasColumnName("secuencial_actual")
            .IsRequired();

        builder.Property(e => e.CertificadoP12Path)
            .HasColumnName("certificado_p12_path")
            .HasMaxLength(ConfiguracionSRI.CertificadoP12PathMaxLen)
            .IsRequired();

        builder.Property(e => e.CertificadoPassword)
            .HasColumnName("certificado_password")
            .HasMaxLength(ConfiguracionSRI.CertificadoPasswordMaxLen)
            .IsRequired();

        builder.Property(e => e.Ambiente)
            .HasColumnName("ambiente")
            .IsRequired();

        builder.Property(e => e.TipoEmision)
            .HasColumnName("tipo_emision")
            .IsRequired();

        builder.Property(e => e.UrlSriAutorizacion)
            .HasColumnName("url_sri_autorizacion")
            .HasMaxLength(ConfiguracionSRI.UrlSriAutorizacionMaxLen)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.RucEmpresa)
            .IsUnique()
            .HasDatabaseName("ix_configuracion_sri_ruc_empresa");
    }
}
