using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Application.Modules.Catalog.UseCases;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

public sealed class SriIceMixedCatalogTests
{
    [Fact]
    public void Read_contract_keeps_both_components_as_numbers_and_publishes_mode_and_unit()
    {
        var dto = new SriIceRateDto("3031", "ICE Bebidas Alcohólicas", 75m, 10.41m,
            "Mixed", "USD por litro de alcohol puro");
        var json = System.Text.Json.JsonSerializer.SerializeToElement(dto,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        json.GetProperty("percentage").GetDecimal().Should().Be(75m);
        json.GetProperty("unitValue").GetDecimal().Should().Be(10.41m);
        json.GetProperty("calculationType").GetString().Should().Be("Mixed");
        json.GetProperty("specificUnit").GetString().Should().Be("USD por litro de alcohol puro");
        json.GetProperty("code").GetString().Should().Be("3031");
    }

    [Theory]
    [InlineData("3031")]
    [InlineData("3033")]
    public void Mixed_concepts_store_both_official_components_without_becoming_selectable(string code)
    {
        var model = new ModelBuilder();
        model.ApplyConfiguration(new SriIceRateConfiguration());
        var row = model.Model.FindEntityType(typeof(SriIceRate))!.GetSeedData()
            .SingleOrDefault(x => (string)x["Code"]! == code);
        row.Should().NotBeNull();
        row!["Percentage"].Should().Be(75m);
        row["UnitValue"].Should().Be(10.41m);
        Convert.ToInt32(row["CalculationType"], System.Globalization.CultureInfo.InvariantCulture).Should().Be(3);
        row["IsActive"].Should().Be(false);
        row["SpecificUnit"].Should().Be("USD por litro de alcohol puro");
        row["NormativeSourceId"].Should().NotBeNull();
    }
}
