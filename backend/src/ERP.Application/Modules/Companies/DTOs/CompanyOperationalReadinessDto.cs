namespace ERP.Application.Modules.Companies.DTOs;

/// <summary>
/// COMPANY-OPERATING-SETUP-01: DTO de salida del checklist de preparación operativa. Espejo 1:1
/// de <see cref="ERP.Domain.Modules.Company.Interfaces.CompanyOperationalReadinessResult"/> — no
/// expone entidades de dominio, solo primitivos/strings de enum. Deliberadamente sin texto: el
/// frontend resuelve label/mensaje/porqué-importa a partir de <c>Code</c> vía i18n.
/// <c>ActionTarget</c> es un identificador semántico de pantalla (p. ej. "Branches",
/// "ElectronicInvoicingSettings") — NUNCA una ruta de SPA; el frontend es el único responsable de
/// traducirlo a la ruta concreta de React Router.
/// </summary>
public sealed record CompanyOperationalReadinessItemDto(
    string Code,
    string Status,
    string Severity,
    string? BlockingArea,
    string? ActionTarget
);

public sealed record CompanyOperationalReadinessSectionDto(
    string Code,
    string Status,
    IReadOnlyList<CompanyOperationalReadinessItemDto> Items
);

public sealed record CompanyOperationalReadinessDto(
    string OverallStatus,
    bool CanSell,
    bool CanIssueElectronicInvoices,
    bool CanUseInventory,
    bool CanUseCashRegister,
    IReadOnlyList<CompanyOperationalReadinessSectionDto> Sections
);
