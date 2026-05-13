using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

public sealed class ExtractoBancarioConfiguration : IEntityTypeConfiguration<ExtractoBancario>
{
    public void Configure(EntityTypeBuilder<ExtractoBancario> builder)
    {
        builder.ToTable("extracto_bancario");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CuentaBancariaId).HasColumnName("cuenta_bancaria_id").IsRequired();
        builder.Property(x => x.PeriodoDesde).HasColumnName("periodo_desde").IsRequired();
        builder.Property(x => x.PeriodoHasta).HasColumnName("periodo_hasta").IsRequired();
        builder.Property(x => x.SaldoInicialExtracto).HasColumnName("saldo_inicial_extracto").HasPrecision(18, 2);
        builder.Property(x => x.SaldoFinalExtracto).HasColumnName("saldo_final_extracto").HasPrecision(18, 2);
        builder.Property(x => x.FechaCarga).HasColumnName("fecha_carga").IsRequired();
        builder.Property(x => x.Conciliado).HasColumnName("conciliado").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<CuentaBancaria>()
            .WithMany()
            .HasForeignKey(x => x.CuentaBancariaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Movimientos)
            .WithOne()
            .HasForeignKey(m => m.ExtractoBancarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.CuentaBancariaId, x.PeriodoDesde, x.PeriodoHasta })
            .HasDatabaseName("ix_extracto_bancario_tenant_cuenta_periodo");
    }
}
