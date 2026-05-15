using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class CajaChicaConfiguration : IEntityTypeConfiguration<CajaChica>
{
    public void Configure(EntityTypeBuilder<CajaChica> builder)
    {
        builder.ToTable("caja_chica");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(CajaChica.NombreMaxLen).IsRequired();
        builder.Property(x => x.SaldoAsignado).HasColumnName("saldo_asignado").HasPrecision(18, 2);
        builder.Property(x => x.SaldoActual).HasColumnName("saldo_actual").HasPrecision(18, 2);
        builder.Property(x => x.CuentaBancariaIdReposicion).HasColumnName("cuenta_bancaria_id_reposicion");
        builder.Property(x => x.CuentaContableCajaId).HasColumnName("cuenta_contable_caja_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<CuentaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.CuentaBancariaIdReposicion)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.Account>()
            .WithMany()
            .HasForeignKey(x => x.CuentaContableCajaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.Nombre })
            .IsUnique()
            .HasDatabaseName("ux_caja_chica_tenant_nombre");
    }
}
