using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// ZH-DESIGN-SYSTEM-PRECISION-04C1 — el % de retención es un porcentaje FISCAL de escala fija
/// (<see cref="FiscalPrecision.Percentage"/>). Un valor con más decimales se rechaza en validación
/// en vez de que el dominio lo redondee en silencio. La regla vive en un único validador de línea,
/// compartido por la emisión directa y por la intención de retención de gastos.
/// </summary>
public sealed class IssueRetentionLineValidatorTests
{
    private readonly IssueRetentionLineValidator _validator = new();

    private static IssueRetentionLineInput Line(decimal rate) =>
        new(RetentionTaxType.Income, "303", BaseAmount: 100m, RetentionRate: rate, RetainedAmount: 10m);

    [Theory]
    [InlineData("12.34")]
    [InlineData("12.3")]
    [InlineData("12")]
    [InlineData("12.340")] // ceros finales no agregan escala efectiva
    [InlineData("1.75")]
    public void RetentionRate_dentro_de_la_escala_fiscal_es_valido(string rate)
    {
        _validator.Validate(Line(decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12.345")]
    [InlineData("0.001")]
    [InlineData("2.7501")]
    public void RetentionRate_con_mas_decimales_que_la_escala_fiscal_es_rechazado(string rate)
    {
        var result = _validator.Validate(
            Line(decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture))
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(IssueRetentionLineInput.RetentionRate)
            && e.ErrorMessage.Contains(FiscalPrecision.Percentage.ToString())
        );
    }

    [Fact]
    public void RetentionIntent_de_gastos_hereda_la_misma_regla()
    {
        var intent = new RetentionIntent(
            AppliesRetention: true,
            EmissionPointId: Guid.NewGuid(),
            IssueDate: new DateOnly(2026, 9, 1),
            Lines: [Line(12.345m)]
        );

        new RetentionIntentValidator().Validate(intent).IsValid.Should().BeFalse();
        new RetentionIntentValidator()
            .Validate(intent with { Lines = [Line(12.34m)] })
            .IsValid.Should().BeTrue();
    }
}
