using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// FLOW-READY-02F.1 — causa raíz original del error "Código ICE '3053' no encontrado o
/// inactivo.": el seed de <c>sri_ice_rate</c> no incluía el código real reportado por un
/// proveedor (Arca Continental, ICE de bebidas azucaradas). Mismo patrón que
/// <c>SriDocTypeSeedAlignmentTests</c> — lee el seed directamente desde la configuración EF
/// aplicada a un <see cref="ModelBuilder"/> aislado, sin conexión real a base de datos.
/// </summary>
public sealed class SriIceAndIrbpnrSeedAlignmentTests
{
    [Fact]
    public void ICE_3053_bebidas_gaseosas_con_azucar_existe_activo_como_Specific()
    {
        var seed = GetSeededIceRate("3053");

        seed.Should().NotBeNull("el código ICE 3053 (Fanta/Arca Continental) debe existir en el catálogo global");
        ((bool)seed!["IsActive"]!).Should().BeTrue();
        ((SriTaxCalculationType)seed["CalculationType"]!).Should().Be(SriTaxCalculationType.Specific);
        // No se hardcodea una tarifa numérica — ver comentario en SriIceRateConfiguration.
        seed["Percentage"].Should().BeNull();
    }

    [Fact]
    public void IRBPNR_5001_existe_activo_como_Specific_con_tarifa_de_0_02()
    {
        var seed = GetSeededIrbpnrRate("5001");

        seed.Should().NotBeNull("el código IRBPNR 5001 debe existir en el catálogo global");
        ((bool)seed!["IsActive"]!).Should().BeTrue();
        ((SriTaxCalculationType)seed["CalculationType"]!).Should().Be(SriTaxCalculationType.Specific);
        ((decimal)seed["UnitValue"]!).Should().Be(0.02m);
    }

    [Fact]
    public void Todos_los_codigos_ICE_historicos_siguen_activos_como_Percentage()
    {
        // Regresión — agregar CalculationType/3053 no debe alterar los 13 códigos existentes.
        string[] historicos =
        [
            "3011", "3021", "3041", "3051", "3071", "3072", "3073",
            "3081", "3082", "3083", "3091", "3101", "3111",
        ];

        foreach (var code in historicos)
        {
            var seed = GetSeededIceRate(code);
            seed.Should().NotBeNull($"el código histórico {code} no debe desaparecer del seed");
            ((SriTaxCalculationType)seed!["CalculationType"]!).Should().Be(SriTaxCalculationType.Percentage);
            ((decimal?)seed["Percentage"]).Should().NotBeNull();
        }
    }

    private static IDictionary<string, object?>? GetSeededIceRate(string code)
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new SriIceRateConfiguration());
        var entityType = modelBuilder.Model.FindEntityType(typeof(SriIceRate))!;
        return entityType
            .GetSeedData()
            .FirstOrDefault(seed => (string)seed[nameof(SriIceRate.Code)]! == code);
    }

    private static IDictionary<string, object?>? GetSeededIrbpnrRate(string code)
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new SriIrbpnrRateConfiguration());
        var entityType = modelBuilder.Model.FindEntityType(typeof(SriIrbpnrRate))!;
        return entityType
            .GetSeedData()
            .FirstOrDefault(seed => (string)seed[nameof(SriIrbpnrRate.Code)]! == code);
    }
}
