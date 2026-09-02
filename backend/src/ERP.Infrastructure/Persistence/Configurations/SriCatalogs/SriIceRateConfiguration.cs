using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriIceRateConfiguration : IEntityTypeConfiguration<SriIceRate>
{
    public void Configure(EntityTypeBuilder<SriIceRate> builder)
    {
        builder.ToTable("sri_ice_rate", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(8, 4);
        builder.Property(x => x.UnitValue).HasColumnName("unit_value").HasPrecision(10, 4);
        builder
            .Property(x => x.CalculationType)
            .HasColumnName("calculation_type")
            .HasConversion<int>()
            .HasDefaultValue(SriTaxCalculationType.Percentage)
            .HasSentinel((SriTaxCalculationType)0)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriIceRate
            {
                Code = "3011",
                Name = "Cigarrillos rubios importados",
                Percentage = 150.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3021",
                Name = "Cigarrillos negros nacionales",
                Percentage = 150.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3041",
                Name = "Bebidas gaseosas con azúcar añadida",
                Percentage = 10.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3051",
                Name = "Bebidas energizantes",
                Percentage = 10.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3071",
                Name = "Perfumes y aguas de tocador",
                Percentage = 20.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3072",
                Name = "Videojuegos",
                Percentage = 35.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3073",
                Name = "Armas de fuego deportivas",
                Percentage = 300.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3081",
                Name = "Vehículos ≤3.5t (hasta USD 30k)",
                Percentage = 5.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3082",
                Name = "Vehículos ≤3.5t (USD 30k–40k)",
                Percentage = 10.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3083",
                Name = "Vehículos ≤3.5t (más de USD 40k)",
                Percentage = 15.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3091",
                Name = "Aviones / helicópteros de uso privado",
                Percentage = 15.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3101",
                Name = "Servicios de televisión pagada",
                Percentage = 15.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            new SriIceRate
            {
                Code = "3111",
                Name = "Bebidas alcohólicas (incl. cerveza)",
                Percentage = 75.00m,
                CalculationType = SriTaxCalculationType.Percentage,
            },
            // FLOW-READY-02F.1 — código real reportado por proveedor (Arca Continental, factura XML
            // Fanta Harmony). Tarifa específica sobre gramos de azúcar (Tabla 18 SRI); no se hardcodea
            // un valor numérico porque (a) las fuentes públicas discrepan entre resoluciones/años
            // (~0.18–0.19 por cada 100g de azúcar) y (b) Compras nunca recalcula ICE específico desde
            // el catálogo — siempre usa el "valor" exacto ya declarado en el XML del proveedor
            // (PurchaseInvoiceDetailTax.Source = Xml). El catálogo solo certifica que el código existe
            // y está activo, y da nombre + tipo de cálculo para mostrarlo correctamente en UI.
            new SriIceRate
            {
                Code = "3053",
                Name = "Bebidas gaseosas con alto contenido de azúcar",
                Percentage = null,
                UnitValue = null,
                CalculationType = SriTaxCalculationType.Specific,
            }
        );
    }
}
