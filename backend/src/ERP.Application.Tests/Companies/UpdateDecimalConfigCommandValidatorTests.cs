using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using FluentAssertions;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// CONFIG-FOUNDATION-P1-01 — un decimal de presentación fuera de rango se rechaza al guardar
/// (0-6, igual que el rango que ya validaba decimalSettingsSchema en frontend). Antes de esta
/// entrega, un valor fuera de rango se clampaba en silencio en el repositorio — ya no.
/// </summary>
public sealed class UpdateDecimalConfigCommandValidatorTests
{
    private readonly UpdateDecimalConfigCommandValidator _validator = new();

    private static UpdateDecimalConfigCommand ValidCommand() =>
        new(SalesUnitPrice: 2, PurchaseUnitPrice: 4, Quantity: 4, Percentage: 2, TotalAmount: 2);

    [Fact]
    public void Comando_dentro_de_rango_es_valido()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(100)]
    public void SalesUnitPrice_fuera_de_rango_se_rechaza(int invalid)
    {
        var cmd = ValidCommand() with { SalesUnitPrice = invalid };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateDecimalConfigCommand.SalesUnitPrice));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Cada_campo_decimal_valida_su_propio_rango(int invalid)
    {
        _validator.Validate(ValidCommand() with { PurchaseUnitPrice = invalid }).IsValid.Should().BeFalse();
        _validator.Validate(ValidCommand() with { Quantity = invalid }).IsValid.Should().BeFalse();
        _validator.Validate(ValidCommand() with { Percentage = invalid }).IsValid.Should().BeFalse();
        _validator.Validate(ValidCommand() with { TotalAmount = invalid }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Limites_inclusivos_0_y_6_son_validos()
    {
        _validator
            .Validate(new UpdateDecimalConfigCommand(0, 0, 0, 0, 0))
            .IsValid.Should()
            .BeTrue();
        _validator
            .Validate(new UpdateDecimalConfigCommand(6, 6, 6, 6, 6))
            .IsValid.Should()
            .BeTrue();
    }
}
