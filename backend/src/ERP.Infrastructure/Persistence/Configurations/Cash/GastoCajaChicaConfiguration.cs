using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class GastoCajaChicaConfiguration : IEntityTypeConfiguration<GastoCajaChica>
{
    public void Configure(EntityTypeBuilder<GastoCajaChica> builder)
    {
        builder.ToTable("gasto_caja_chica");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CajaChicaId).HasColumnName("caja_chica_id").IsRequired();
        builder.Property(x => x.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(x => x.Concepto).HasColumnName("concepto").HasMaxLength(GastoCajaChica.ConceptoMaxLen).IsRequired();
        builder.Property(x => x.Monto).HasColumnName("monto").HasPrecision(18, 2);
        builder.Property(x => x.TipoComprobante).HasColumnName("tipo_comprobante").HasMaxLength(GastoCajaChica.TipoComprobanteMaxLen).IsRequired();
        builder.Property(x => x.NumeroComprobante).HasColumnName("numero_comprobante").HasMaxLength(GastoCajaChica.NumeroComprobanteMaxLen);
        builder.Property(x => x.AsientoContableId).HasColumnName("asiento_contable_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<CajaChica>()
            .WithMany()
            .HasForeignKey(x => x.CajaChicaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.AsientoContableId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
