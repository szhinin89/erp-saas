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

        seed.Should()
            .NotBeNull(
                "el código ICE 3053 (Fanta/Arca Continental) debe existir en el catálogo global"
            );
        ((bool)seed!["IsActive"]!).Should().BeTrue();
        ((SriTaxCalculationType)seed["CalculationType"]!)
            .Should()
            .Be(SriTaxCalculationType.Specific);
        // No se hardcodea una tarifa numérica — ver comentario en SriIceRateConfiguration.
        seed["Percentage"].Should().BeNull();
    }

    [Fact]
    public void IRBPNR_5001_existe_activo_como_Specific_con_tarifa_de_0_02()
    {
        var seed = GetSeededIrbpnrRate("5001");

        seed.Should().NotBeNull("el código IRBPNR 5001 debe existir en el catálogo global");
        ((bool)seed!["IsActive"]!).Should().BeTrue();
        ((SriTaxCalculationType)seed["CalculationType"]!)
            .Should()
            .Be(SriTaxCalculationType.Specific);
        ((decimal)seed["UnitValue"]!).Should().Be(0.02m);
    }

    [Fact]
    public void Ningun_codigo_ICE_historico_desaparece_del_seed()
    {
        // Las claves existentes se conservan aunque cambien su definición oficial o su
        // disponibilidad. El contenido vigente se verifica en SriIceTable18CatalogTests.
        string[] historicos =
        [
            "3011",
            "3021",
            "3041",
            "3051",
            "3053",
            "3071",
            "3072",
            "3073",
            "3081",
            "3082",
            "3083",
            "3091",
            "3101",
            "3111",
        ];

        foreach (var code in historicos)
            GetSeededIceRate(code).Should().NotBeNull($"el código histórico {code} no debe desaparecer del seed");
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
