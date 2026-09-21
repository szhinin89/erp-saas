namespace ERP.Domain.Configuration.Entities;

/// <summary>Naturaleza de un campo de la política de precisión.</summary>
public enum PrecisionFieldKind
{
    /// <summary>Cantidad de decimales (entero) de un valor unitario, cantidad, factor o porcentaje.</summary>
    Decimals = 1,

    /// <summary>Un monto (no una cantidad de decimales), p. ej. la tolerancia de cuadre.</summary>
    Amount = 2,
}

/// <summary>
/// Definición de UN campo de <see cref="CompanyPrecisionPolicy"/>: su key pública (la del DTO/API),
/// rango permitido y el valor que le asigna cada perfil predefinido.
/// </summary>
public sealed record PrecisionFieldDefinition(
    string Key,
    PrecisionFieldKind Kind,
    decimal Min,
    decimal Max,
    decimal Standard,
    decimal HighPrecision
);

/// <summary>
/// ERP-PRECISION-POLICY-SSOT-CLEANUP-04: ÚNICA definición productiva de keys, rangos y perfiles
/// predefinidos (Estándar comercial / Alta precisión) de la política de precisión por empresa.
/// La consumen la entidad (factories + validación de rango), el validator de la API, los CHECK de
/// base de datos (<c>CompanyPrecisionPolicyConfiguration</c>), el endpoint de metadata y el
/// frontend (vía ese endpoint). El perfil Personalizado no tiene valores propios: es "cualquier
/// valor dentro de los rangos".
///
/// Nunca define precisión fiscal — eso es <see cref="ERP.Domain.Common.FiscalPrecision"/>.
/// </summary>
public static class PrecisionPolicyDefinitions
{
    public const string SalesUnitPriceDecimals = "salesUnitPriceDecimals";
    public const string PurchaseUnitPriceDecimals = "purchaseUnitPriceDecimals";
    public const string QuantityDecimals = "quantityDecimals";
    public const string PercentageDecimals = "percentageDecimals";
    public const string UnitCostDecimals = "unitCostDecimals";
    public const string AverageCostDecimals = "averageCostDecimals";
    public const string ConversionFactorDecimals = "conversionFactorDecimals";
    public const string SettlementToleranceAmount = "settlementToleranceAmount";

    public static IReadOnlyList<PrecisionFieldDefinition> Fields { get; } =
    [
        new(SalesUnitPriceDecimals, PrecisionFieldKind.Decimals, 2, 6, 2, 4),
        new(PurchaseUnitPriceDecimals, PrecisionFieldKind.Decimals, 2, 10, 4, 6),
        new(QuantityDecimals, PrecisionFieldKind.Decimals, 0, 6, 4, 6),
        new(PercentageDecimals, PrecisionFieldKind.Decimals, 2, 6, 2, 4),
        new(UnitCostDecimals, PrecisionFieldKind.Decimals, 2, 10, 6, 6),
        new(AverageCostDecimals, PrecisionFieldKind.Decimals, 2, 10, 6, 6),
        new(ConversionFactorDecimals, PrecisionFieldKind.Decimals, 2, 10, 6, 8),
        new(SettlementToleranceAmount, PrecisionFieldKind.Amount, 0.00m, 0.02m, 0.01m, 0.01m),
    ];

    public static PrecisionFieldDefinition Get(string key) =>
        Fields.Single(f => f.Key == key);

    public static PrecisionPolicyValues Standard { get; } = BuildValues(f => f.Standard);

    public static PrecisionPolicyValues HighPrecision { get; } = BuildValues(f => f.HighPrecision);

    private static PrecisionPolicyValues BuildValues(Func<PrecisionFieldDefinition, decimal> pick) =>
        new(
            (short)pick(Get(SalesUnitPriceDecimals)),
            (short)pick(Get(PurchaseUnitPriceDecimals)),
            (short)pick(Get(QuantityDecimals)),
            (short)pick(Get(PercentageDecimals)),
            (short)pick(Get(UnitCostDecimals)),
            (short)pick(Get(AverageCostDecimals)),
            (short)pick(Get(ConversionFactorDecimals)),
            pick(Get(SettlementToleranceAmount))
        );

    /// <summary>Texto SQL del CHECK de una columna, derivado del rango de su definición.</summary>
    public static string CheckSql(string column, string key)
    {
        var d = Get(key);
        return d.Kind == PrecisionFieldKind.Amount
            ? FormattableString.Invariant($"{column} BETWEEN {d.Min:0.00} AND {d.Max:0.00}")
            : FormattableString.Invariant($"{column} BETWEEN {d.Min:0} AND {d.Max:0}");
    }
}
