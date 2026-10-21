using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriTaxRegimeConfiguration : IEntityTypeConfiguration<SriTaxRegime>
{
    public void Configure(EntityTypeBuilder<SriTaxRegime> builder)
    {
        builder.ToTable("sri_tax_regime", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Abbrev).HasColumnName("abbrev").HasMaxLength(20);
        // ValueGeneratedNever: conserva false con DEFAULT true (ADR-042).
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).ValueGeneratedNever();

        builder.HasData(
            new SriTaxRegime
            {
                Code = "01",
                Name = "Régimen General",
                Abbrev = "GENERAL",
                IsActive = true,
            },
            new SriTaxRegime
            {
                Code = "02",
                // Ficha 2.34, historial 2.22 y Anexo 22: RIMPE Emprendedor. Abbrev conserva la clave interna.
                Name = "RIMPE – Emprendedor",
                Abbrev = "RIMPE_ME",
                IsActive = true,
            },
            new SriTaxRegime
            {
                Code = "03",
                Name = "RIMPE – Negocio Popular",
                Abbrev = "RIMPE_NP",
                IsActive = true,
            },
            new SriTaxRegime
            {
                // Calificación, no régimen: la ficha 2.34 la emite como número de resolución
                // (contribuyenteEspecial, Company.SpecialTaxpayerNo). Se conserva para referencias.
                Code = "04",
                Name = "Contribuyente Especial",
                Abbrev = "ESP",
                IsActive = false,
            }
        );
    }
}
