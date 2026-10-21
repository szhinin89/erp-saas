using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

public sealed class SriIceOfficialCodesTests
{
    // 3630 keeps its corrected meaning and value; its 2026 rate is unverified, so it is not selectable.
    [Theory]
    [InlineData("3073", "ICE Vehículos Motorizados cuyo PVP sea hasta de 20000 USD", 5, true)]
    [InlineData("3630", "ICE Armas de Fuego, Armas deportivas y Municiones", 30, false)]
    public void Codes_match_ficha_234_table_18(string code, string name, int percentage, bool isActive)
    {
        var model = new ModelBuilder();
        model.ApplyConfiguration(new SriIceRateConfiguration());
        var row = model.Model.FindEntityType(typeof(SriIceRate))!.GetSeedData()
            .SingleOrDefault(x => (string)x["Code"]! == code);
        row.Should().NotBeNull();
        row!["Name"].Should().Be(name);
        row["Percentage"].Should().Be((decimal)percentage);
        row["CalculationType"].Should().Be(SriTaxCalculationType.Percentage);
        row["IsActive"].Should().Be(isActive);
    }
}
