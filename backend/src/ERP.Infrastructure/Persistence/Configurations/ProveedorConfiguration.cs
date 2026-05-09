using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Proveedores.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("proveedores");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(p => p.TipoPersona)
            .HasColumnName("tipo_persona")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.RazonSocial)
            .HasColumnName("razon_social")
            .HasMaxLength(Proveedor.RazonSocialMaxLen)
            .IsRequired();

        builder.Property(p => p.Ruc)
            .HasColumnName("ruc")
            .HasMaxLength(Proveedor.RucMaxLen)
            .IsRequired();

        builder.Property(p => p.Correo)
            .HasColumnName("correo")
            .HasMaxLength(Proveedor.CorreoMaxLen);

        builder.Property(p => p.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(Proveedor.TelefonoMaxLen);

        builder.Property(p => p.Direccion)
            .HasColumnName("direccion")
            .HasMaxLength(Proveedor.DireccionMaxLen);

        builder.Property(p => p.CondicionPago)
            .HasColumnName("condicion_pago")
            .HasMaxLength(Proveedor.CondicionPagoMaxLen)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        // RUC único por tenant
        builder.HasIndex(p => new { p.TenantId, p.Ruc })
            .IsUnique()
            .HasDatabaseName("ix_proveedores_tenant_ruc");

        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("ix_proveedores_tenant_id");
    }
}
