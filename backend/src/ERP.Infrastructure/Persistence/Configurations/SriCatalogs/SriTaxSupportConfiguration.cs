using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriTaxSupportConfiguration : IEntityTypeConfiguration<SriTaxSupport>
{
    public void Configure(EntityTypeBuilder<SriTaxSupport> builder)
    {
        builder.ToTable("sri_tax_support", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).ValueGeneratedNever();

        // Catálogo ATS 2026-08-06, TABLAS REFERENCIALES, Tabla 5.
        // IsActive controls selection only; historical document codes are never remapped.
        builder.HasData(
            new SriTaxSupport
            {
                Code = "00",
                Name = "Casos especiales cuyo sustento no aplica en las opciones anteriores",
                IsActive = false,
            },
            new SriTaxSupport
            {
                Code = "01",
                Name = "Crédito Tributario para declaración de IVA (servicios y bienes distintos de inventarios y activos fijos)",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "02",
                Name = "Costo o Gasto para declaración de IR (servicios y bienes distintos de inventarios y activos fijos)",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "03",
                Name = "Activo Fijo - Crédito Tributario para declaración de IVA",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "04",
                Name = "Activo Fijo - Costo o Gasto para declaración de IR",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "05",
                Name = "Liquidación Gastos de Viaje, hospedaje y alimentación Gastos IR (a nombre de empleados y no de la empresa)",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "06",
                Name = "Inventario - Crédito Tributario para declaración de IVA",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "07",
                Name = "Inventario - Costo o Gasto para declaración de IR",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "08",
                Name = "Valor pagado para solicitar Reembolso de Gasto (intermediario)",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "09",
                Name = "Reembolso por Siniestros",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "10",
                Name = "Distribución de Dividendos, Beneficios o Utilidades",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "11",
                Name = "Convenios de débito o recaudación para IFI´s",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "12",
                Name = "Impuestos y retenciones presuntivos",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "13",
                Name = "Valores reconocidos por entidades del sector público a favor de sujetos pasivos",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "14",
                Name = "Valores facturados por socios a operadoras de transporte (que no constituyen gasto de dicha operadora)",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "15",
                Name = "Pagos efectuados por consumos propios y de terceros de servicios digitales",
                IsActive = true,
            },
            new SriTaxSupport
            {
                Code = "16",
                Name = "Provisiones de cuentas incobrables",
                IsActive = false,
            },
            new SriTaxSupport
            {
                Code = "17",
                Name = "Nota de crédito deducible",
                IsActive = false,
            },
            new SriTaxSupport
            {
                Code = "18",
                Name = "Importaciones",
                IsActive = false,
            },
            new SriTaxSupport
            {
                Code = "19",
                Name = "Reembolso de gastos",
                IsActive = false,
            },
            new SriTaxSupport
            {
                Code = "20",
                Name = "Notas de crédito por devoluciones",
                IsActive = false,
            }
        );
    }
}
