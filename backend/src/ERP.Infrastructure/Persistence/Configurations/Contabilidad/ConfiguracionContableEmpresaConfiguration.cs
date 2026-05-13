using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Contabilidad.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionContableEmpresaConfiguration : IEntityTypeConfiguration<ConfiguracionContableEmpresa>
{
    public void Configure(EntityTypeBuilder<ConfiguracionContableEmpresa> builder)
    {
        builder.ToTable("configuracion_contable_empresa");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.CuentaInventarioId).HasColumnName("cuenta_inventario_id");
        builder.Property(e => e.CuentaCostoVentaId).HasColumnName("cuenta_costo_venta_id");
        builder.Property(e => e.CuentaProveedoresId).HasColumnName("cuenta_proveedores_id");
        builder.Property(e => e.CuentaVentasId).HasColumnName("cuenta_ventas_id");
        builder.Property(e => e.CuentaClientesId).HasColumnName("cuenta_clientes_id");
        builder.Property(e => e.CuentaIvaComprasId).HasColumnName("cuenta_iva_compras_id");
        builder.Property(e => e.CuentaIvaVentasId).HasColumnName("cuenta_iva_ventas_id");
        builder.Property(e => e.CuentaEfectivoId).HasColumnName("cuenta_efectivo_id");
        builder.Property(e => e.CuentaBancoId).HasColumnName("cuenta_banco_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_config_contable_empresa_tenant");

        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaInventarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaCostoVentaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaProveedoresId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaVentasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaClientesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaIvaComprasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaIvaVentasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaEfectivoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaBancoId).OnDelete(DeleteBehavior.Restrict);
    }
}
