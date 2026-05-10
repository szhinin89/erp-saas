using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Compras.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class OrdenCompraFacturaConfiguration : IEntityTypeConfiguration<OrdenCompraFactura>
{
    public void Configure(EntityTypeBuilder<OrdenCompraFactura> builder)
    {
        builder.ToTable("ordenes_compra_facturas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.OrdenCompraId).HasColumnName("orden_compra_id").IsRequired();
        builder.Property(e => e.CompraFacturaId).HasColumnName("compra_factura_id").IsRequired();
        builder.Property(e => e.FechaVinculacion).HasColumnName("fecha_vinculacion").IsRequired();
        builder.Property(e => e.VinculadoPor).HasColumnName("vinculado_por").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.OrdenCompraId, e.CompraFacturaId })
            .IsUnique()
            .HasDatabaseName("ix_ordenes_compra_facturas_tenant_oc_factura");

        builder.HasIndex(e => new { e.TenantId, e.OrdenCompraId })
            .HasDatabaseName("ix_ordenes_compra_facturas_tenant_orden");
    }
}
