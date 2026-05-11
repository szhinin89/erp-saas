using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class VentasNotaDetalleConfiguration : IEntityTypeConfiguration<VentasNotaDetalle>
{
    public void Configure(EntityTypeBuilder<VentasNotaDetalle> builder)
    {
        builder.ToTable("ventas_nota_detalles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.VentasNotaCreditoDebitoId).HasColumnName("ventas_nota_credito_debito_id").IsRequired();
        builder.Property(e => e.ProductoId).HasColumnName("producto_id").IsRequired();

        builder.Property(e => e.Cantidad).HasColumnName("cantidad").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Impuesto).HasColumnName("impuesto").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(e => e.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(VentasNotaDetalle.DescripcionMaxLen)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.VentasNotaCreditoDebitoId })
            .HasDatabaseName("ix_ventas_nota_detalles_tenant_nota");
    }
}
