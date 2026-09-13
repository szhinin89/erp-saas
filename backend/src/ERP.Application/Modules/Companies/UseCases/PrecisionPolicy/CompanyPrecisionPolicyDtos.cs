namespace ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Objeto único que el frontend consume para toda precisión
/// numérica relevante a una empresa: los 7 campos operativos + tolerancia de cuadre (editable,
/// respaldados por <c>company_precision_policy</c>) y 3 campos fiscales/contables fijos del
/// sistema (<see cref="ERP.Domain.Common.FiscalPrecision"/>, NUNCA editables desde aquí, incluidos
/// solo para que el frontend no tenga que combinar dos fuentes).
/// </summary>
public sealed record EffectivePrecisionPolicyDto(
    string ProfileType,
    int SalesUnitPriceDecimals,
    int PurchaseUnitPriceDecimals,
    int QuantityDecimals,
    int PercentageDecimals,
    int UnitCostDecimals,
    int AverageCostDecimals,
    int ConversionFactorDecimals,
    decimal SettlementToleranceAmount,
    bool IsLocked,
    DateTime? LockedAt,
    string? LockedReason,
    // ── Fijos del sistema — leídos de FiscalPrecision, nunca de company_precision_policy ──
    int MoneyDecimals,
    int TaxDecimals,
    int AccountingDecimals
);

/// <summary>Payload de entrada para actualizar la policy. ProfileType Standard/HighPrecision ignora los campos individuales.</summary>
public sealed record UpdateCompanyPrecisionPolicyInput(
    string ProfileType,
    int SalesUnitPriceDecimals,
    int PurchaseUnitPriceDecimals,
    int QuantityDecimals,
    int PercentageDecimals,
    int UnitCostDecimals,
    int AverageCostDecimals,
    int ConversionFactorDecimals,
    decimal SettlementToleranceAmount
);
