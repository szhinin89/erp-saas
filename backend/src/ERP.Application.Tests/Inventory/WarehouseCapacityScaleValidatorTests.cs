using System.Globalization;
using ERP.Application.Modules.Inventory.Warehouses.UseCases;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.CreateWarehouse;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.UpdateWarehouse;
using ERP.Domain.Modules.Inventory.Entities;
using FluentAssertions;
using FluentValidation.Results;

namespace ERP.Application.Tests.Inventory;

/// <summary>
/// ZH-BACKEND-PRECISION-HARDENING-01 — la capacidad de bodega tiene escala contractual
/// <see cref="WarehousePrecision.Capacity"/> (<c>numeric(18,4)</c>). Un valor con más decimales se
/// rechaza en Create y Update, antes de que PostgreSQL lo redondee en silencio.
/// </summary>
public sealed class WarehouseCapacityScaleValidatorTests
{
    private static readonly CreateWarehouseCommandValidator CreateValidator = new();
    private static readonly UpdateWarehouseCommandValidator UpdateValidator = new();

    private static IEnumerable<ValidationResult> ValidateBoth(decimal? capacity) =>
        [
            CreateValidator.Validate(
                new CreateWarehouseCommand(Guid.NewGuid(), "Bodega", null, null, null, null, null, null, null, capacity, null)
            ),
            UpdateValidator.Validate(
                new UpdateWarehouseCommand(Guid.NewGuid(), Guid.NewGuid(), "Bodega", null, null, null, null, null, null, null, capacity, null)
            ),
        ];

    [Theory]
    [InlineData("0")]
    [InlineData("100.1234")]
    [InlineData("100.12340")] // ceros finales no agregan escala efectiva
    [InlineData("100.1")]
    public void Capacidad_dentro_de_la_escala_es_valida(string capacity)
    {
        foreach (var result in ValidateBoth(decimal.Parse(capacity, CultureInfo.InvariantCulture)))
            result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Capacidad_nula_sigue_siendo_valida()
    {
        foreach (var result in ValidateBoth(null))
            result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("100.12345")]
    [InlineData("0.00001")]
    public void Capacidad_con_mas_decimales_que_la_escala_es_rechazada(string capacity)
    {
        foreach (var result in ValidateBoth(decimal.Parse(capacity, CultureInfo.InvariantCulture)))
        {
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e =>
                e.PropertyName == "Capacity" && e.ErrorMessage == WarehouseCapacityRules.ScaleMessage
            );
        }
    }

    [Fact]
    public void Regla_existente_no_negativa_intacta()
    {
        foreach (var result in ValidateBoth(-1m))
            result.Errors.Should().Contain(e => e.ErrorMessage == "La capacidad no puede ser negativa.");
    }
}
