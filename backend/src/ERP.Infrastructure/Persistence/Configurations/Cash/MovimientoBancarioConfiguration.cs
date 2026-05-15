using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class MovimientoBancarioConfiguration : IEntityTypeConfiguration<MovimientoBancario>
{
    public void Configure(EntityTypeBuilder<MovimientoBancario> builder)
    {
        builder.ToTable("movimiento_bancario");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ExtractoBancarioId).HasColumnName("extracto_bancario_id").IsRequired();
        builder.Property(x => x.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(MovimientoBancario.DescripcionMaxLen).IsRequired();
        builder.Property(x => x.Monto).HasColumnName("monto").HasPrecision(18, 2);
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(MovimientoBancario.TipoMaxLen).IsRequired();
        builder.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(MovimientoBancario.ReferenciaMaxLen);
        builder.Property(x => x.AsientoContableId).HasColumnName("asiento_contable_id");
        builder.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(MovimientoBancario.EstadoMaxLen).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ExtractoBancarioId, x.Fecha })
            .HasDatabaseName("ix_movimiento_bancario_tenant_extracto_fecha");

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.AsientoContableId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
