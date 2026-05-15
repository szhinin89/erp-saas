using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class CuentaBancariaConfiguration : IEntityTypeConfiguration<CuentaBancaria>
{
    public void Configure(EntityTypeBuilder<CuentaBancaria> builder)
    {
        builder.ToTable("cuenta_bancaria");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(CuentaBancaria.NombreMaxLen).IsRequired();
        builder.Property(x => x.NumeroCuenta).HasColumnName("numero_cuenta").HasMaxLength(CuentaBancaria.NumeroCuentaMaxLen).IsRequired();
        builder.Property(x => x.TipoCuenta).HasColumnName("tipo_cuenta").HasMaxLength(CuentaBancaria.TipoCuentaMaxLen).IsRequired();
        builder.Property(x => x.Moneda).HasColumnName("moneda").HasMaxLength(CuentaBancaria.MonedaMaxLen).IsRequired();
        builder.Property(x => x.SaldoInicial).HasColumnName("saldo_inicial").HasPrecision(18, 2);
        builder.Property(x => x.SaldoActual).HasColumnName("saldo_actual").HasPrecision(18, 2);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CuentaContableId).HasColumnName("cuenta_contable_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.NumeroCuenta })
            .IsUnique()
            .HasDatabaseName("ux_cuenta_bancaria_tenant_numero");

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.Account>()
            .WithMany()
            .HasForeignKey(x => x.CuentaContableId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
