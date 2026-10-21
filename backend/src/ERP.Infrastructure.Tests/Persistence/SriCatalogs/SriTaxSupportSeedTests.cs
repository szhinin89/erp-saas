using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

public sealed class SriTaxSupportSeedTests
{
    // SRI Catalogo_ATS.xls, 2026-08-06, Tabla 5; only surrounding spreadsheet whitespace trimmed.
    internal static readonly Dictionary<string, string> Official = new()
    {
        ["01"] = "Crédito Tributario para declaración de IVA (servicios y bienes distintos de inventarios y activos fijos)",
        ["02"] = "Costo o Gasto para declaración de IR (servicios y bienes distintos de inventarios y activos fijos)",
        ["03"] = "Activo Fijo - Crédito Tributario para declaración de IVA",
        ["04"] = "Activo Fijo - Costo o Gasto para declaración de IR",
        ["05"] = "Liquidación Gastos de Viaje, hospedaje y alimentación Gastos IR (a nombre de empleados y no de la empresa)",
        ["06"] = "Inventario - Crédito Tributario para declaración de IVA",
        ["07"] = "Inventario - Costo o Gasto para declaración de IR",
        ["08"] = "Valor pagado para solicitar Reembolso de Gasto (intermediario)",
        ["09"] = "Reembolso por Siniestros",
        ["10"] = "Distribución de Dividendos, Beneficios o Utilidades",
        ["11"] = "Convenios de débito o recaudación para IFI´s",
        ["12"] = "Impuestos y retenciones presuntivos",
        ["13"] = "Valores reconocidos por entidades del sector público a favor de sujetos pasivos",
        ["14"] = "Valores facturados por socios a operadoras de transporte (que no constituyen gasto de dicha operadora)",
        ["15"] = "Pagos efectuados por consumos propios y de terceros de servicios digitales",
    };

    [Fact]
    public void Seleccionables_coinciden_exactamente_con_la_tabla_5()
    {
        var builder = new ModelBuilder();
        builder.ApplyConfiguration(new SriTaxSupportConfiguration());
        var seed = builder.Model.FindEntityType(typeof(SriTaxSupport))!.GetSeedData().ToList();
        seed.Where(x => (bool)x["IsActive"]!).ToDictionary(x => (string)x["Code"]!, x => (string)x["Name"]!)
            .Should().BeEquivalentTo(Official);
        seed.Where(x => !(bool)x["IsActive"]!).Select(x => (string)x["Code"]!)
            .Should().BeEquivalentTo("00", "16", "17", "18", "19", "20");
    }
}
