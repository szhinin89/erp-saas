using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriIrbpnrRateConfiguration : IEntityTypeConfiguration<SriIrbpnrRate>
{
    public void Configure(EntityTypeBuilder<SriIrbpnrRate> builder)
    {
        builder.ToTable("sri_irbpnr_rate", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(8, 4);
        builder.Property(x => x.UnitValue).HasColumnName("unit_value").HasPrecision(10, 4);
        builder
            .Property(x => x.CalculationType)
            .HasColumnName("calculation_type")
            .HasConversion<int>()
            .HasDefaultValue(SriTaxCalculationType.Specific)
            .HasSentinel((SriTaxCalculationType)0)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        // FLOW-READY-02F.1 — código real reportado por proveedor (Arca Continental). Tarifa fija por
        // ley (Ley de Fomento Ambiental y Optimización de los Ingresos del Estado, 2011): USD 0.02 por
        // botella plástica no retornable, monto estable desde su creación (a diferencia del ICE
        // ad-valorem, no está sujeto a resoluciones anuales del SRI) — corroborado por fuentes públicas
        // independientes, no inventado. Compras siempre usa el "valor" exacto del XML, nunca recalcula
        // desde este catálogo — este valor es referencial/informativo para la UI.
        builder.HasData(
            new SriIrbpnrRate
            {
                Code = "5001",
                Name = "Impuesto Redimible a las Botellas Plásticas No Retornables",
                Percentage = null,
                UnitValue = 0.02m,
                CalculationType = SriTaxCalculationType.Specific,
            }
        );
    }
}
