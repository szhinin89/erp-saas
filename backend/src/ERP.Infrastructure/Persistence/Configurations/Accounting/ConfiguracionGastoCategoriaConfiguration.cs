using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionGastoCategoriaConfiguration : IEntityTypeConfiguration<ConfiguracionGastoCategoria>
{
    public void Configure(EntityTypeBuilder<ConfiguracionGastoCategoria> builder)
    {
        builder.ToTable("configuracion_gasto_categoria");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Categoria)
            .HasColumnName("categoria")
            .HasMaxLength(ConfiguracionGastoCategoria.CategoriaMaxLen)
            .IsRequired();
        builder.Property(e => e.CuentaGastoId).HasColumnName("cuenta_gasto_id").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.Categoria })
            .IsUnique()
            .HasDatabaseName("ux_config_gasto_categoria_tenant_cat");

        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CuentaGastoId).OnDelete(DeleteBehavior.Restrict);
    }
}
