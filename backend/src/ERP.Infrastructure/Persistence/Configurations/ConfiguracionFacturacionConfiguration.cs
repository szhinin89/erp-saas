using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Configuration.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionFacturacionConfiguration : IEntityTypeConfiguration<ConfiguracionFacturacion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionFacturacion> builder)
    {
        builder.ToTable("configuracion_facturacion");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.RazonSocial)
            .HasColumnName("razon_social")
            .HasMaxLength(ConfiguracionFacturacion.RazonSocialMaxLen)
            .IsRequired();

        builder.Property(e => e.NombreComercial)
            .HasColumnName("nombre_comercial")
            .HasMaxLength(ConfiguracionFacturacion.NombreComercialMaxLen)
            .IsRequired();

        builder.Property(e => e.Ruc)
            .HasColumnName("ruc")
            .HasMaxLength(ConfiguracionFacturacion.RucMaxLen)
            .IsRequired();

        builder.Property(e => e.DireccionMatriz)
            .HasColumnName("direccion_matriz")
            .HasMaxLength(ConfiguracionFacturacion.DireccionMatrizMaxLen)
            .IsRequired();

        builder.Property(e => e.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(ConfiguracionFacturacion.TelefonoMaxLen)
            .IsRequired();

        builder.Property(e => e.Correo)
            .HasColumnName("correo")
            .HasMaxLength(ConfiguracionFacturacion.CorreoMaxLen);

        builder.Property(e => e.ObligadoContabilidad)
            .HasColumnName("obligado_contabilidad")
            .IsRequired();

        builder.Property(e => e.ContribuyenteEspecial)
            .HasColumnName("contribuyente_especial")
            .HasMaxLength(ConfiguracionFacturacion.ContribuyenteEspecialMaxLen);

        builder.Property(e => e.LogoBase64)
            .HasColumnName("logo_base64")
            .HasMaxLength(ConfiguracionFacturacion.LogoBase64MaxLen);

        builder.Property(e => e.LeyendaAdicional)
            .HasColumnName("leyenda_adicional")
            .HasMaxLength(ConfiguracionFacturacion.LeyendaAdicionalMaxLen);

        builder.Property(e => e.AnchoTirilla)
            .HasColumnName("ancho_tirilla")
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("ix_configuracion_facturacion_tenant");
    }
}
