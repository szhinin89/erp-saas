using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>
/// Catálogo ICE frente a la Ficha técnica offline 2.34, Tabla 18 (pp. 30-32), y a las tarifas 2026
/// (Res. NAC-DGERCGC25-00000040; LRTI art. 82). Definición vigente: architecture.md § ICE.
/// </summary>
public sealed class SriIceTable18CatalogTests
{
    // Códigos únicos publicados en la Tabla 18 (3041, 3541 y 3671 aparecen en varias filas).
    private static readonly string[] Table18Codes =
    [
        "3011", "3021", "3023", "3031", "3033", "3041", "3043", "3053", "3054", "3073", "3075", "3077",
        "3078", "3079", "3080", "3081", "3092", "3093", "3101", "3111", "3532", "3533", "3541", "3542",
        "3543", "3544", "3545", "3552", "3553", "3581", "3582", "3601", "3602", "3610", "3620", "3630",
        "3640", "3660", "3671", "3680", "3681", "3682", "3683", "3684", "3685", "3686", "3687", "3688",
        "3689", "3690", "3691", "3692", "3693", "3694", "3695", "3696", "3697", "3698", "3699", "3710",
        "3720", "3730", "3740", "3771", "3871", "3873", "3874", "3875", "3876", "3877", "3878",
    ];

    private static readonly string[] LegacyCodesOutsideTable18 = ["3051", "3071", "3072", "3082", "3083", "3091"];

    [Fact]
    public void Catalog_contains_every_table_18_code_plus_preserved_legacy_keys_only()
    {
        Table18Codes.Should().HaveCount(71).And.OnlyHaveUniqueItems();
        Rates().Select(x => x.Code).Should().BeEquivalentTo(Table18Codes.Concat(LegacyCodesOutsideTable18));
        Rates().Where(x => LegacyCodesOutsideTable18.Contains(x.Code)).Should().OnlyContain(x =>
            !x.IsActive && x.NormativeSourceId == SriNormativeSourceConfiguration.FichaV234Table18Id);
    }

    [Theory]
    [InlineData("3011", "ICE Cigarrillos Rubios", SriTaxCalculationType.Specific, true)]
    [InlineData("3021", "ICE Cigarrillos Negros", SriTaxCalculationType.Specific, true)]
    [InlineData("3041", "ICE Cerveza Industrial", SriTaxCalculationType.Mixed, false)]
    [InlineData("3081", "ICE Aviones, Tricars, yates, Barcos de Recreo", SriTaxCalculationType.Percentage, true)]
    [InlineData("3101", "ICE Bebidas Energizantes", SriTaxCalculationType.Percentage, true)]
    [InlineData("3111", "ICE Bebidas No Alcohólicas", SriTaxCalculationType.Specific, true)]
    public void Codes_with_a_wrong_former_meaning_follow_table_18(
        string code, string name, SriTaxCalculationType type, bool isActive)
    {
        var row = Rates().Single(x => x.Code == code);
        row.Name.Should().Be(name);
        row.CalculationType.Should().Be(type);
        row.IsActive.Should().Be(isActive);
    }

    [Theory]
    [InlineData("3031", 10.41)]
    [InlineData("3033", 10.41)]
    [InlineData("3041", 13.62)]
    [InlineData("3043", 1.56)]
    [InlineData("3532", 10.41)]
    [InlineData("3533", 10.41)]
    [InlineData("3541", 13.62)]
    [InlineData("3545", 1.56)]
    public void Alcohol_and_beer_keep_both_2026_components_and_stay_unselectable(string code, double specific)
    {
        var row = Rates().Single(x => x.Code == code);
        row.CalculationType.Should().Be(SriTaxCalculationType.Mixed);
        row.Percentage.Should().Be(75m);
        row.UnitValue.Should().Be((decimal)specific);
        row.SpecificUnit.Should().Be("USD por litro de alcohol puro");
        row.IsActive.Should().BeFalse();
        AssertFiscal2026(row);
    }

    [Theory]
    [InlineData("3542", "USD por unidad de cigarrillo", 0.16)]
    [InlineData("3543", "USD por unidad de cigarrillo", 0.16)]
    [InlineData("3552", "USD por 100 gramos de azúcar añadida", 0.18)]
    [InlineData("3602", "USD por 100 gramos de azúcar añadida", 0.18)]
    [InlineData("3680", "USD por funda plástica", 0.08)]
    public void New_specific_codes_record_the_2026_tariff_and_stay_unselectable(string code, string unit, double value)
    {
        var row = Rates().Single(x => x.Code == code);
        row.CalculationType.Should().Be(SriTaxCalculationType.Specific);
        row.UnitValue.Should().Be((decimal)value);
        row.SpecificUnit.Should().Be(unit);
        row.IsActive.Should().BeFalse();
        AssertFiscal2026(row);
    }

    [Theory]
    [InlineData("3011", "USD por unidad de cigarrillo")]
    [InlineData("3021", "USD por unidad de cigarrillo")]
    [InlineData("3053", "USD por 100 gramos de azúcar añadida")]
    [InlineData("3111", "USD por 100 gramos de azúcar añadida")]
    public void Selectable_specific_codes_never_expose_a_unit_value_that_consumers_multiply_by_quantity(
        string code, string unit)
    {
        var row = Rates().Single(x => x.Code == code);
        row.IsActive.Should().BeTrue("Compras importa el monto exacto del XML del proveedor con este código");
        row.UnitValue.Should().BeNull();
        row.Percentage.Should().BeNull();
        row.SpecificUnit.Should().Be(unit);
        AssertFiscal2026(row);
    }

    [Fact]
    public void Every_selectable_row_is_computable_and_every_row_is_traceable()
    {
        foreach (var row in Rates())
        {
            row.NormativeSourceId.Should().NotBeNull($"{row.Code} requiere fuente normativa");
            if (row.ValidFrom is not null && row.ValidUntil is not null)
                row.ValidFrom.Should().BeOnOrBefore(row.ValidUntil.Value);
            if (!row.IsActive)
                continue;
            row.CalculationType.Should().NotBe(SriTaxCalculationType.Mixed);
            if (row.CalculationType == SriTaxCalculationType.Percentage)
                row.Percentage.Should().NotBeNull($"{row.Code} activo requiere tarifa verificada");
            else
                row.UnitValue.Should().BeNull($"{row.Code}: la base física no es la cantidad del ítem");
        }
        Rates().Count(x => x.IsActive).Should().Be(38);
    }

    [Theory]
    [InlineData("3092", 15)] // Tabla 18 publica 0%; art. 82 grupo III vigente: 15%.
    [InlineData("3660", 30)] // Tabla 18 publica 35%; art. 82 grupo III vigente: 30%.
    [InlineData("3023", 150)]
    [InlineData("3054", 10)]
    [InlineData("3610", 20)]
    [InlineData("3640", 100)]
    [InlineData("3080", 35)]
    public void Ad_valorem_rates_follow_article_82_over_the_2023_table_columns(string code, int percentage)
    {
        var row = Rates().Single(x => x.Code == code);
        row.Percentage.Should().Be(percentage);
        row.IsActive.Should().BeTrue();
        row.NormativeSourceId.Should().Be(SriNormativeSourceConfiguration.LrtiArticle82Id);
        row.ValidFrom.Should().BeNull("la fecha de inicio de la tarifa no fue verificada");
    }

    [Theory]
    [InlineData("3682")]
    [InlineData("3683")]
    [InlineData("3081")]
    [InlineData("3878")]
    [InlineData("3581")]
    [InlineData("3582")]
    public void Rates_restored_by_the_annulment_of_decree_645_start_on_2024_05_21(string code)
    {
        var row = Rates().Single(x => x.Code == code);
        row.Percentage.Should().Be(code is "3682" or "3683" ? 150m : 15m);
        row.ValidFrom.Should().Be(new DateOnly(2024, 5, 21));
        row.NormativeSourceId.Should().Be(SriNormativeSourceConfiguration.IceRestored20240521Id);
    }

    [Fact]
    public void Codes_without_a_current_tax_or_verified_rate_are_never_selectable()
    {
        string[] withoutRate =
        [
            "3620", "3720", "3671", "3771", "3093", "3681", "3730", "3688", "3689", "3690", "3691", "3692",
            "3693", "3694", "3695", "3696", "3697", "3698", "3699",
        ];
        foreach (var code in withoutRate)
        {
            var row = Rates().Single(x => x.Code == code);
            row.IsActive.Should().BeFalse();
            row.Percentage.Should().BeNull();
            row.UnitValue.Should().BeNull();
        }
        Rates().Single(x => x.Code == "3630").IsActive.Should().BeFalse("la tarifa 2026 de armas no fue verificada");
    }

    [Fact]
    public void Sources_name_the_baseline_that_introduces_them()
    {
        var model = new ModelBuilder();
        model.ApplyConfiguration(new SriNormativeSourceConfiguration());
        var sources = model.Model.FindEntityType(typeof(SriNormativeSource))!.GetSeedData()
            .ToDictionary(x => (Guid)x["Id"]!);
        Guid[] ids =
        [
            SriNormativeSourceConfiguration.FichaV234Table18Id, SriNormativeSourceConfiguration.LrtiArticle82Id,
            SriNormativeSourceConfiguration.IceRestored20240521Id, SriNormativeSourceConfiguration.IceSriSummaryMatrixId,
        ];
        foreach (var id in ids)
        {
            sources[id]["IntroducedInMigration"].Should().Be(SriNormativeSourceConfiguration.IntroducedInMigration);
            ((string)sources[id]["ReferenceUrl"]!).Should().StartWith("https://www.");
            ((string)sources[id]["Notes"]!).Length.Should().BeLessThanOrEqualTo(SriNormativeSource.NotesMaxLen);
        }
        var resolution = (string)sources[SriNormativeSourceConfiguration.IceSpecific2026Id]["Notes"]!;
        resolution.Should().Contain("0,16").And.Contain("10,41").And.Contain("1,56").And.Contain("13,62")
            .And.Contain("0,18").And.Contain("0,08").And.Contain("2.34");
        resolution.Length.Should().BeLessThanOrEqualTo(SriNormativeSource.NotesMaxLen);
    }

    private static void AssertFiscal2026(SriIceRate row)
    {
        row.NormativeSourceId.Should().Be(SriNormativeSourceConfiguration.IceSpecific2026Id);
        row.ValidFrom.Should().Be(new DateOnly(2026, 1, 1));
        row.ValidUntil.Should().Be(new DateOnly(2026, 12, 31));
    }

    private static List<SriIceRate> Rates()
    {
        var model = new ModelBuilder();
        model.ApplyConfiguration(new SriIceRateConfiguration());
        return model.Model.FindEntityType(typeof(SriIceRate))!.GetSeedData().Select(d => new SriIceRate
        {
            Code = (string)d[nameof(SriIceRate.Code)]!,
            Name = (string)d[nameof(SriIceRate.Name)]!,
            Percentage = (decimal?)d[nameof(SriIceRate.Percentage)],
            UnitValue = (decimal?)d[nameof(SriIceRate.UnitValue)],
            SpecificUnit = (string?)d[nameof(SriIceRate.SpecificUnit)],
            NormativeSourceId = (Guid?)d[nameof(SriIceRate.NormativeSourceId)],
            ValidFrom = (DateOnly?)d[nameof(SriIceRate.ValidFrom)],
            ValidUntil = (DateOnly?)d[nameof(SriIceRate.ValidUntil)],
            CalculationType = (SriTaxCalculationType)d[nameof(SriIceRate.CalculationType)]!,
            IsActive = (bool)d[nameof(SriIceRate.IsActive)]!,
        }).ToList();
    }
}
