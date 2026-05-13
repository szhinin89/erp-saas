using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Inventario.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class KardexSnapshotConfiguration : IEntityTypeConfiguration<KardexSnapshot>
{
    public void Configure(EntityTypeBuilder<KardexSnapshot> builder)
    {
        builder.ToTable("kardex_snapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id").IsRequired();

        builder.Property(s => s.ProductoId)
            .HasColumnName("producto_id").IsRequired();

        builder.Property(s => s.BodegaId)
            .HasColumnName("bodega_id").IsRequired();

        builder.Property(s => s.FechaSnapshot)
            .HasColumnName("fecha_snapshot")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(s => s.CantidadSaldo)
            .HasColumnName("cantidad_saldo")
            .HasPrecision(18, 6).IsRequired();

        builder.Property(s => s.ValorSaldo)
            .HasColumnName("valor_saldo")
            .HasPrecision(18, 6).IsRequired();

        builder.Property(s => s.CostoPromedio)
            .HasColumnName("costo_promedio")
            .HasPrecision(18, 6).IsRequired();

        builder.Property(s => s.ComputadoEn)
            .HasColumnName("computado_en").IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.ProductoId, s.BodegaId, s.FechaSnapshot })
            .IsUnique()
            .HasDatabaseName("ix_kardex_snapshots_lookup");

        builder.HasIndex(s => new { s.TenantId, s.FechaSnapshot })
            .HasDatabaseName("ix_kardex_snapshots_tenant_fecha");

        // El filtro global de tenant no aplica aquí (la tabla no implementa ITenantEntity
        // con QueryFilter) — el repositorio filtra explícitamente por tenantId.
    }
}
