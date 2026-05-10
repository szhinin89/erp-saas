using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Inventario.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class TransferenciaDetalleConfiguration : IEntityTypeConfiguration<TransferenciaDetalle>
{
    public void Configure(EntityTypeBuilder<TransferenciaDetalle> builder)
    {
        builder.ToTable("transferencia_detalles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.TransferenciaId).HasColumnName("transferencia_id").IsRequired();
        builder.Property(e => e.ProductoId).HasColumnName("producto_id").IsRequired();

        builder.Property(e => e.Cantidad)
            .HasColumnName("cantidad")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(e => e.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(TransferenciaDetalle.DescripcionMaxLen)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.TransferenciaId })
            .HasDatabaseName("ix_transferencia_detalles_tenant_transferencia");
    }
}
