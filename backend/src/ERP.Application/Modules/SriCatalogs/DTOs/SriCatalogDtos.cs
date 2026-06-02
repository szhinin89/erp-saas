namespace ERP.Application.Modules.SriCatalogs.DTOs;

// ── Formas de Pago ────────────────────────────────────────────────────────────
public sealed record SriPaymentMethodDto(string Code, string Name, bool IsActive);

// ── Tipos de Comprobante ──────────────────────────────────────────────────────
public sealed record SriDocTypeDto(string Code, string Name, string ShortName, bool IsElectronic, bool IsActive);

// ── Tipos de Identificación ───────────────────────────────────────────────────
public sealed record SriIdTypeDto(string Code, string Name, short? Digits);

// ── Tarifas IVA ───────────────────────────────────────────────────────────────
public sealed record SriVatRateDto(string Code, string Name, decimal Percentage, bool IsActive, DateOnly? ValidFrom, DateOnly? ValidUntil);

// ── Tarifas ICE ───────────────────────────────────────────────────────────────
public sealed record SriIceRateDto(string Code, string Name, decimal? Percentage, decimal? UnitValue, bool IsActive);

// ── Retenciones ───────────────────────────────────────────────────────────────
public sealed record SriRetentionCodeDto(string TaxType, string Code, string Name, decimal Percentage, string AppliesTo, bool IsActive);

// ── Países ────────────────────────────────────────────────────────────────────
public sealed record SriCountryDto(string Code, string? Iso2, string Name, string? PhoneCode, bool IsActive);

// ── Regímenes Tributarios ─────────────────────────────────────────────────────
public sealed record SriTaxRegimeDto(string Code, string Name, string? Abbrev, bool IsActive);

// ── Sustentos Tributarios ─────────────────────────────────────────────────────
public sealed record SriTaxSupportDto(string Code, string Name, bool IsActive);

// ── Unidades de Medida ────────────────────────────────────────────────────────
public sealed record SriUomDto(string Code, string Name, string? Abbrev, bool IsActive);

// ── Ambientes ─────────────────────────────────────────────────────────────────
public sealed record SriEnvironmentDto(short Code, string Name, string Abbrev);

// ── Tipos de Emisión ──────────────────────────────────────────────────────────
public sealed record SriEmissionTypeDto(short Code, string Name);

// ── Códigos de Error ──────────────────────────────────────────────────────────
public sealed record SriErrorCodeDto(string Code, string ErrorType, string Name, string? Description, bool IsActive);
