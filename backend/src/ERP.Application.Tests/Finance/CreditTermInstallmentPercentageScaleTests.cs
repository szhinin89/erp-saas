using System.Globalization;
using ERP.Application.Modules.Finance.UseCases.CreditTerms;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// ZH-BACKEND-PRECISION-HARDENING-01 — el % de cuota tiene escala contractual
/// <see cref="CreditTermsPrecision.InstallmentPercentage"/> (<c>numeric(5,2)</c>). Un valor con más
/// decimales se rechaza en validación, antes de que PostgreSQL lo redondee en silencio.
/// </summary>
public sealed class CreditTermInstallmentPercentageScaleTests
{
    private static readonly CreateCreditTermCommandValidator CreateValidator = new();
    private static readonly UpdateCreditTermCommandValidator UpdateValidator = new();

    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static CreateCreditTermCommand Create(params decimal[] percentages) =>
        new(
            "CT-90",
            "90 días",
            CreditTermMode.FinancialStrict,
            90,
            percentages.Select((p, i) => new InstallmentInput(i + 1, (i + 1) * 30, p)).ToList()
        );

    private static UpdateCreditTermCommand Update(params decimal[] percentages) =>
        new(
            Guid.NewGuid(),
            "90 días",
            CreditTermMode.FinancialStrict,
            90,
            percentages.Select((p, i) => new InstallmentInput(i + 1, (i + 1) * 30, p)).ToList()
        );

    [Theory]
    [InlineData("33.33")]
    [InlineData("33.330")] // ceros finales no agregan escala efectiva
    [InlineData("33.3")]
    [InlineData("34")]
    public void Porcentaje_dentro_de_la_escala_es_valido(string pct)
    {
        CreateValidator.Validate(Create(D(pct))).IsValid.Should().BeTrue();
        UpdateValidator.Validate(Update(D(pct))).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("33.335")]
    [InlineData("0.001")]
    [InlineData("50.0001")]
    public void Porcentaje_con_mas_decimales_que_la_escala_es_rechazado(string pct)
    {
        var create = CreateValidator.Validate(Create(D(pct)));
        var update = UpdateValidator.Validate(Update(D(pct)));

        foreach (var result in new[] { create, update })
        {
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Installments[0].Percentage"
                && e.ErrorMessage == InstallmentPercentageRules.ScaleMessage
            );
        }
    }

    [Fact]
    public void Caso_detectado_33_335_x2_mas_33_33_falla_por_escala_antes_de_persistir()
    {
        // En memoria suma exactamente 100 — el dominio lo aceptaría — pero numeric(5,2) lo guardaría
        // como 33.34 + 33.34 + 33.33 = 100.01. Debe cortarse en el validador.
        var percentages = new[] { 33.335m, 33.335m, 33.33m };
        percentages.Sum().Should().Be(100m);
        var act = () =>
            CreditTerm.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "CT-90",
                "90 días",
                CreditTermMode.FinancialStrict,
                90,
                Guid.NewGuid(),
                percentages.Select((p, i) => (i + 1, (i + 1) * 30, p))
            );
        act.Should().NotThrow("la suma en memoria es 100 exacto: el dominio no detecta el problema");

        foreach (var result in new[] { CreateValidator.Validate(Create(percentages)), UpdateValidator.Validate(Update(percentages)) })
        {
            result.IsValid.Should().BeFalse();
            result.Errors.Where(e => e.ErrorMessage == InstallmentPercentageRules.ScaleMessage)
                .Select(e => e.PropertyName)
                .Should()
                .BeEquivalentTo("Installments[0].Percentage", "Installments[1].Percentage");
        }
    }

    [Fact]
    public void Reglas_existentes_intactas_rango_por_cuota()
    {
        CreateValidator.Validate(Create(0m)).IsValid.Should().BeFalse();
        CreateValidator.Validate(Create(100.01m)).IsValid.Should().BeFalse();
        CreateValidator.Validate(Create(100m)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Reglas_existentes_intactas_suma_total_100_en_dominio()
    {
        CreateValidator.Validate(Create(33.33m, 33.33m, 33.34m)).IsValid.Should().BeTrue();

        var act = () =>
            CreditTerm.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "CT-90",
                "90 días",
                CreditTermMode.FinancialStrict,
                90,
                Guid.NewGuid(),
                new[] { (1, 30, 33.33m), (2, 60, 33.33m), (3, 90, 33.33m) }
            );
        act.Should().Throw<InvalidOperationException>().WithMessage("*exactamente 100%*");
    }
}
