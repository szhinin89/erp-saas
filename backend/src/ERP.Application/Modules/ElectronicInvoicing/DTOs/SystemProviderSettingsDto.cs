namespace ERP.Application.Modules.ElectronicInvoicing.DTOs;

/// <summary>
/// ERP-CORE-CLOSEOUT-09 — datos del proveedor del sistema de facturación electrónica (instancia
/// completa, no por empresa/tenant). Ver <see cref="ERP.Domain.Configuration.Entities.SystemProviderSettings"/>.
/// </summary>
public sealed record SystemProviderSettingsDto(
    string? Ruc,
    string? LegalName,
    string? CiiuCode,
    bool Enabled,
    DateOnly? EffectiveDate,
    bool IsFullyConfigured,
    DateTime? UpdatedAtUtc
);
