using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CompraNotaProveedorDetalleConfiguration : IEntityTypeConfiguration<CompraNotaProveedorDetalle>
{
    public void Configure(EntityTypeBuilder<CompraNotaProveedorDetalle> builder)
    {
        builder.ToTable("compra_nota_proveedor_detalles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompraNotaProveedorId).HasColumnName("compra_nota_proveedor_id").IsRequired();
        builder.Property(x => x.ProductoId).HasColumnName("producto_id");

        builder.Property(x => x.CodigoPrincipalProveedor)
            .HasColumnName("codigo_principal_proveedor")
            .HasMaxLength(CompraNotaProveedorDetalle.CodigoPrincipalMaxLen);

        builder.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(CompraNotaProveedorDetalle.DescripcionMaxLen)
            .IsRequired();

        builder.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Impuesto).HasColumnName("impuesto").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.CompraNotaProveedorId })
            .HasDatabaseName("ix_compra_nota_prov_det_tenant_nota");
    }
}
