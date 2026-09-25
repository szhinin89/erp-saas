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
    int AccountingDecimals,
    // ZH-DESIGN-SYSTEM-PRECISION-04C1 — escala fija de porcentajes FISCALES (p. ej. % de retención
    // SRI), leída de FiscalPrecision.Percentage. No configurable ni persistida; distinta de
    // PercentageDecimals (porcentajes operativos configurables por empresa).
    int FiscalPercentageDecimals
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

/// <summary>
/// ERP-PRECISION-POLICY-SSOT-CLEANUP-04: un campo de la política — key pública, tipo, rango y valor
/// por defecto (el del perfil Estándar comercial). Sale de <c>PrecisionPolicyDefinitions</c>.
/// </summary>
public sealed record PrecisionFieldMetadataDto(
    string Key,
    string Kind,
    decimal Min,
    decimal Max,
    decimal DefaultValue
);

/// <summary>Perfil predefinido con los valores que fija para cada key. Personalizado no aparece: no tiene valores propios.</summary>
public sealed record PrecisionProfileMetadataDto(
    string ProfileType,
    IReadOnlyDictionary<string, decimal> Values
);

/// <summary>Definiciones + perfiles predefinidos — metadata estática, igual para todas las empresas.</summary>
public sealed record PrecisionPolicyMetadataDto(
    IReadOnlyList<PrecisionFieldMetadataDto> Fields,
    IReadOnlyList<PrecisionProfileMetadataDto> Profiles
);
