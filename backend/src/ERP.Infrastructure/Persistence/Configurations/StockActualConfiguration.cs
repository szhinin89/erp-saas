using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Inventario.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class StockActualConfiguration : IEntityTypeConfiguration<StockActual>
{
    public void Configure(EntityTypeBuilder<StockActual> builder)
    {
        builder.ToTable("stock_actual");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.ProductoId)
            .HasColumnName("producto_id")
            .IsRequired();

        builder.Property(s => s.BodegaId)
            .HasColumnName("bodega_id")
            .IsRequired();

        builder.Property(s => s.Cantidad)
            .HasColumnName("cantidad")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(s => s.CantidadReservada)
            .HasColumnName("cantidad_reservada")
            .HasPrecision(18, 6)
            .IsRequired();

        // Propiedad calculada en C# — no persiste en BD
        builder.Ignore(s => s.CantidadDisponible);

        builder.Property(s => s.UltimaActualizacion)
            .HasColumnName("ultima_actualizacion")
            .IsRequired();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        // Un único registro por producto + bodega por tenant
        builder.HasIndex(s => new { s.TenantId, s.ProductoId, s.BodegaId })
            .IsUnique()
            .HasDatabaseName("ix_stock_actual_tenant_producto_bodega");
    }
}
