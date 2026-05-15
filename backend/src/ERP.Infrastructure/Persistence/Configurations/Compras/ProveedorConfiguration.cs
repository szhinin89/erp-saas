using ERP.Domain.Modules.Compras.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        // Tabla renombrada de 'proveedores' → 'supplier' (sin datos reales)
        builder.ToTable("supplier");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();

        // Columnas renombradas a inglés
        builder.Property(p => p.TipoPersona)
            .HasColumnName("person_type")
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(p => p.RazonSocial)
            .HasColumnName("legal_name")
            .HasMaxLength(Proveedor.RazonSocialMaxLen)
            .IsRequired();
        builder.Property(p => p.Ruc)
            .HasColumnName("ruc")
            .HasMaxLength(Proveedor.RucMaxLen)
            .IsRequired();
        builder.Property(p => p.Correo)
            .HasColumnName("email")
            .HasMaxLength(Proveedor.CorreoMaxLen);
        builder.Property(p => p.Telefono)
            .HasColumnName("phone")
            .HasMaxLength(Proveedor.TelefonoMaxLen);
        builder.Property(p => p.Direccion)
            .HasColumnName("address")
            .HasMaxLength(Proveedor.DireccionMaxLen);
        builder.Property(p => p.CondicionPago)
            .HasColumnName("payment_terms")
            .HasMaxLength(Proveedor.CondicionPagoMaxLen)
            .IsRequired();

        // ── Campos SRI / comerciales ─────────────────────────────
        builder.Property(p => p.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(3)
            .IsFixedLength();
        builder.Property(p => p.TaxSupportCode)
            .HasColumnName("tax_support_code")
            .HasMaxLength(5);
        builder.Property(p => p.PaymentDays)
            .HasColumnName("payment_days")
            .HasDefaultValue((short)0);
        builder.Property(p => p.CreditLimit)
            .HasColumnName("credit_limit")
            .HasPrecision(18, 2);

        builder.Ignore(p => p.SriIdTypeCode);

        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(p => new { p.TenantId, p.Ruc })
            .IsUnique()
            .HasDatabaseName("uq_supplier_ruc");
        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("ix_supplier_tenant_id");
    }
}
