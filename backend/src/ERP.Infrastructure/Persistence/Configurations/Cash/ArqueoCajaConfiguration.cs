using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class ArqueoCajaConfiguration : IEntityTypeConfiguration<ArqueoCaja>
{
    public void Configure(EntityTypeBuilder<ArqueoCaja> builder)
    {
        builder.ToTable("arqueo_caja");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CajaChicaId).HasColumnName("caja_chica_id").IsRequired();
        builder.Property(x => x.FechaArqueo).HasColumnName("fecha_arqueo").IsRequired();
        builder.Property(x => x.EfectivoFisico).HasColumnName("efectivo_fisico").HasPrecision(18, 2);
        builder.Property(x => x.Diferencia).HasColumnName("diferencia").HasPrecision(18, 2);
        builder.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(ArqueoCaja.ObservacionesMaxLen);
        builder.Property(x => x.Aprobado).HasColumnName("aprobado").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<CajaChica>()
            .WithMany()
            .HasForeignKey(x => x.CajaChicaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
