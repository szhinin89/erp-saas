using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriVatRateConfiguration : IEntityTypeConfiguration<SriVatRate>
{
    public void Configure(EntityTypeBuilder<SriVatRate> builder)
    {
        builder.ToTable("sri_vat_rate", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(6, 2);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");

        // CAT-01 (auditoría SRI): los códigos 4, 5, 7, 8 y 10 estaban mal mapeados contra la
        // Tabla 17 (Tarifa IVA) de la Ficha Técnica del SRI — Esquema Offline v2.32, y el
        // código 11 no existe en el catálogo oficial. Tabla 17 real: 0→0%, 2→12%, 3→14%,
        // 4→15%, 5→5%, 6→No objeto de Impuesto, 7→Exento de IVA, 8→IVA diferenciado, 10→13%.
        // Antes de este fix, code "10" (usado por el motor de cálculo para el 15% general
        // vigente) declaraba al SRI <codigoPorcentaje>10</codigoPorcentaje>, que el SRI
        // interpreta como 13% — no como 15%. Ver informe de auditoría, hallazgo CAT-01.
        builder.HasData(
            new SriVatRate { Code = "0", Name = "0% IVA", Percentage = 0.00m, IsActive = true, ValidFrom = new DateOnly(2008, 1, 1), ValidUntil = null },
            new SriVatRate { Code = "2", Name = "12% IVA (histórico)", Percentage = 12.00m, IsActive = false, ValidFrom = new DateOnly(2008, 1, 1), ValidUntil = new DateOnly(2016, 5, 31) },
            new SriVatRate { Code = "3", Name = "14% IVA (histórico)", Percentage = 14.00m, IsActive = false, ValidFrom = new DateOnly(2016, 6, 1), ValidUntil = new DateOnly(2017, 5, 31) },
            new SriVatRate { Code = "4", Name = "15% IVA (tarifa general vigente)", Percentage = 15.00m, IsActive = true, ValidFrom = new DateOnly(2024, 4, 1), ValidUntil = null },
            new SriVatRate { Code = "5", Name = "5% IVA", Percentage = 5.00m, IsActive = true, ValidFrom = new DateOnly(2024, 1, 1), ValidUntil = null },
            new SriVatRate { Code = "6", Name = "No objeto de Impuesto", Percentage = 0.00m, IsActive = true, ValidFrom = new DateOnly(2008, 1, 1), ValidUntil = null },
            new SriVatRate { Code = "7", Name = "Exento de IVA", Percentage = 0.00m, IsActive = true, ValidFrom = new DateOnly(2008, 1, 1), ValidUntil = null },
            new SriVatRate { Code = "8", Name = "IVA diferenciado (tarifa variable por decreto — turismo)", Percentage = 8.00m, IsActive = true, ValidFrom = new DateOnly(2008, 1, 1), ValidUntil = null },
            new SriVatRate { Code = "10", Name = "13% IVA", Percentage = 13.00m, IsActive = true, ValidFrom = new DateOnly(2008, 1, 1), ValidUntil = null }
            // Code "11" ("13% IVA transitorio") eliminado: no existe en la Tabla 17 oficial del SRI.
        );
    }
}
