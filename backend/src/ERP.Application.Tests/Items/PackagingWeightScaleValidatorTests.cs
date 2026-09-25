using System.Globalization;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
using ERP.Domain.Modules.Items.Entities;
using FluentAssertions;

namespace ERP.Application.Tests.Items;

/// <summary>
/// ZH-BACKEND-PRECISION-HARDENING-01 — el peso por nivel de empaque tiene escala contractual
/// <see cref="ItemPrecision.PackagingWeight"/> (<c>numeric(10,3)</c>). Un valor con más decimales se
/// rechaza en validación, antes de que PostgreSQL lo redondee en silencio.
/// </summary>
public sealed class PackagingWeightScaleValidatorTests
{
    private static readonly ReplaceItemPackagingLevelsCommandValidator Validator = new();

    private static ReplaceItemPackagingLevelsCommand Command(decimal? weight) =>
        new(Guid.NewGuid(), [new PackagingLevelInput(null, "UNIDAD", 1, 1m, "UNIT", Weight: weight, IsBaseUnit: true)]);

    [Theory]
    [InlineData("1.234")]
    [InlineData("1.2340")] // ceros finales no agregan escala efectiva
    [InlineData("1")]
    public void Peso_dentro_de_la_escala_es_valido(string weight)
    {
        Validator.Validate(Command(decimal.Parse(weight, CultureInfo.InvariantCulture)))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Peso_nulo_sigue_siendo_valido()
    {
        Validator.Validate(Command(null)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1.2345")]
    [InlineData("0.0001")]
    public void Peso_con_mas_decimales_que_la_escala_es_rechazado(string weight)
    {
        var result = Validator.Validate(Command(decimal.Parse(weight, CultureInfo.InvariantCulture)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Levels[0].Weight"
            && e.ErrorMessage == ReplaceItemPackagingLevelsCommandValidator.PackagingWeightScaleMessage
        );
    }
}
