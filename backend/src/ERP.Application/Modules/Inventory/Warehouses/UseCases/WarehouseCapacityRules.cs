using ERP.Domain.Modules.Inventory.Entities;
using FluentValidation;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases;

/// <summary>
/// ZH-BACKEND-PRECISION-HARDENING-01 — escala de la capacidad de bodega (SSOT
/// <see cref="WarehousePrecision.Capacity"/>, columna <c>numeric(18,4)</c>). Un valor con más
/// decimales se rechaza aquí, nunca lo redondea PostgreSQL en silencio. Compartida por Create/Update.
/// </summary>
public static class WarehouseCapacityRules
{
    public static readonly string ScaleMessage =
        $"La capacidad admite como máximo {WarehousePrecision.Capacity} decimales.";

    public static IRuleBuilderOptions<T, decimal?> WithinCapacityScale<T>(
        this IRuleBuilder<T, decimal?> ruleBuilder
    ) =>
        ruleBuilder
            .Must(v => v is null || decimal.Round(v.Value, WarehousePrecision.Capacity) == v.Value)
            .WithMessage(ScaleMessage);
}
