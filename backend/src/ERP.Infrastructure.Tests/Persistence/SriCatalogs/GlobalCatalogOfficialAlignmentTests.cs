using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>
/// Correcciones GLOBAL frente a la ficha técnica offline 2.34 y al Catálogo ATS 06/08/2026.
/// Definición vigente: docs/architecture/architecture.md § GLOBAL IMMUTABLE.
/// </summary>
public sealed class GlobalCatalogOfficialAlignmentTests
{
    [Fact]
    public void Placa_09_is_kept_but_never_selectable_because_table_6_and_the_xsd_only_admit_04_to_08()
    {
        var usages = Seed<SriIdTypeUsage>(new SriIdTypeUsageConfiguration());
        usages.Where(x => (string)x[nameof(SriIdTypeUsage.IdTypeCode)]! == "09")
            .Should().HaveCount(2).And.OnlyContain(x => (bool)x[nameof(SriIdTypeUsage.IsActive)]! == false);
        usages.Where(x => (string)x[nameof(SriIdTypeUsage.IdTypeCode)]! != "09")
            .Should().HaveCount(11).And.OnlyContain(x => (bool)x[nameof(SriIdTypeUsage.IsActive)]! == true);
    }

    [Fact]
    public void Tax_regimes_follow_annex_22_and_special_taxpayer_is_not_a_regime()
    {
        var regimes = Seed<SriTaxRegime>(new SriTaxRegimeConfiguration())
            .ToDictionary(x => (string)x[nameof(SriTaxRegime.Code)]!);
        regimes["02"][nameof(SriTaxRegime.Name)].Should().Be("RIMPE – Emprendedor");
        regimes["02"][nameof(SriTaxRegime.Abbrev)].Should().Be("RIMPE_ME", "la clave interna no cambia");
        regimes["03"][nameof(SriTaxRegime.Name)].Should().Be("RIMPE – Negocio Popular");
        regimes["04"][nameof(SriTaxRegime.IsActive)].Should().Be(false);
        regimes.Values.Count(x => (bool)x[nameof(SriTaxRegime.IsActive)]!).Should().Be(3);
    }

    [Theory]
    [InlineData("02", "Nota o boleta de venta")]
    [InlineData("08", "Boletos o entradas a espectáculos públicos")]
    [InlineData("09", "Tiquetes o vales emitidos por máquinas registradoras")]
    [InlineData("18", "Documentos autorizados utilizados en ventas excepto N/C N/D")]
    public void Non_electronic_document_types_use_the_ats_table_4_name(string code, string name)
    {
        var row = Seed<SriDocType>(new SriDocTypeConfiguration()).Single(x => (string)x[nameof(SriDocType.Code)]! == code);
        row[nameof(SriDocType.Name)].Should().Be(name);
        row[nameof(SriDocType.IsElectronic)].Should().Be(false);
        row[nameof(SriDocType.IsActive)].Should().Be(false);
    }

    [Theory]
    [InlineData("2", "RUC DEL EMISOR SE ENCUENTRA NO ACTIVO")]
    [InlineData("10", "ESTABLECIMIENTO DEL EMISOR SE ENCUENTRA CLAUSURADO")]
    [InlineData("26", "TAMAÑO MÁXIMO SUPERADO")]
    public void Error_codes_missing_from_section_11_are_present(string code, string name)
    {
        var row = Seed<SriErrorCode>(new SriErrorCodeConfiguration()).Single(x => (string)x[nameof(SriErrorCode.Code)]! == code);
        row[nameof(SriErrorCode.Name)].Should().Be(name);
        row[nameof(SriErrorCode.ErrorType)].Should().Be("ERROR");
        ((string)row[nameof(SriErrorCode.Description)]!).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Every_country_carries_its_table_25_code()
    {
        var expected = new Dictionary<string, string>
        {
            ["ARG"] = "101", ["AUS"] = "501", ["BOL"] = "102", ["BRA"] = "103", ["CAN"] = "104", ["CHL"] = "108",
            ["CHN"] = "331", ["COL"] = "105", ["CRI"] = "106", ["DEU"] = "202", ["DOM"] = "122", ["ECU"] = "593",
            ["ESP"] = "209", ["FRA"] = "211", ["GBR"] = "213", ["GTM"] = "111", ["HND"] = "113", ["IND"] = "309",
            ["ITA"] = "219", ["JPN"] = "314", ["MEX"] = "116", ["NIC"] = "117", ["PAN"] = "118", ["PER"] = "120",
            ["PRY"] = "119", ["SLV"] = "123", ["URY"] = "125", ["USA"] = "110", ["VEN"] = "126",
        };
        Seed<SriCountry>(new SriCountryConfiguration())
            .ToDictionary(x => (string)x[nameof(SriCountry.Code)]!, x => (string?)x[nameof(SriCountry.SriCode)])
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Isd_keeps_its_current_rate_without_version_and_is_not_selectable()
    {
        var isd = Seed<SriRetentionCode>(new SriRetentionCodeConfiguration())
            .Single(x => (string)x[nameof(SriRetentionCode.TaxType)]! == "ISD");
        isd[nameof(SriRetentionCode.Code)].Should().Be("4580");
        isd[nameof(SriRetentionCode.Percentage)].Should().Be(5.00m);
        isd[nameof(SriRetentionCode.IsActive)].Should().Be(false);
        Seed<SriRetentionCodeVersion>(new SriRetentionCodeVersionConfiguration())
            .Should().NotContain(x => (Guid)x[nameof(SriRetentionCodeVersion.RetentionCodeId)]! == (Guid)isd[nameof(SriRetentionCode.Id)]!);
    }

    private static List<IDictionary<string, object?>> Seed<T>(IEntityTypeConfiguration<T> configuration)
        where T : class
    {
        var model = new ModelBuilder();
        model.ApplyConfiguration(configuration);
        return model.Model.FindEntityType(typeof(T))!.GetSeedData().ToList();
    }
}
