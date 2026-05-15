using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class TransferenciaConfiguration : IEntityTypeConfiguration<Transferencia>
{
    public void Configure(EntityTypeBuilder<Transferencia> builder)
    {
        builder.ToTable("transferencias");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(e => e.Secuencial).HasColumnName("secuencial").IsRequired();

        builder.Property(e => e.NumeroTransferencia)
            .HasColumnName("numero_transferencia")
            .HasMaxLength(Transferencia.NumeroMaxLen)
            .IsRequired();

        builder.Property(e => e.BodegaOrigenId).HasColumnName("bodega_origen_id").IsRequired();
        builder.Property(e => e.BodegaDestinoId).HasColumnName("bodega_destino_id").IsRequired();

        builder.Property(e => e.FechaTransferencia).HasColumnName("fecha_transferencia").IsRequired();

        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(Transferencia.EstadoMaxLen)
            .IsRequired();

        builder.Property(e => e.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(Transferencia.MotivoMaxLen);

        builder.Property(e => e.Observaciones)
            .HasColumnName("observaciones")
            .HasMaxLength(Transferencia.ObservacionMaxLen);

        builder.Property(e => e.FechaConfirmacion).HasColumnName("fecha_confirmacion");
        builder.Property(e => e.ConfirmadoPor).HasColumnName("confirmado_por");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.BodegaOrigen)
            .WithMany()
            .HasForeignKey(e => e.BodegaOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.BodegaDestino)
            .WithMany()
            .HasForeignKey(e => e.BodegaDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Detalles)
            .WithOne()
            .HasForeignKey(d => d.TransferenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.NumeroTransferencia })
            .IsUnique()
            .HasDatabaseName("ix_transferencias_tenant_numero");

        builder.HasIndex(e => new { e.TenantId, e.Estado })
            .HasDatabaseName("ix_transferencias_tenant_estado");
    }
}
