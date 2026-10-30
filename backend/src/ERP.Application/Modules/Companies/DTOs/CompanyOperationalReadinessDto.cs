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
    IReadOnlyList<string> BlockingAreas,
    string? ActionTarget
);

public sealed record CompanyOperationalReadinessSectionDto(
    string Code,
    string Status,
    IReadOnlyList<CompanyOperationalReadinessItemDto> Items
);

/// <summary>
/// Una caja activa de la sucursal: su punto de emisión, modalidad, estado operativo y fiscal, y los
/// <c>Code</c> de los ítems de <see cref="CompanyOperationalReadinessDto.Sections"/> que incumple
/// (referencias, no copias del requisito).
/// </summary>
public sealed record CompanyOperationalReadinessCashRegisterDto(
    Guid CashRegisterId,
    string Code,
    string Name,
    Guid? EmissionPointId,
    string? EstablishmentCode,
    string? EmissionPointCode,
    string? EmissionType,
    string OperationalStatus,
    string FiscalStatus,
    IReadOnlyList<string> RequirementCodes
);

public sealed record CompanyOperationalReadinessDto(
    Guid BranchId,
    string OverallStatus,
    bool CanSell,
    bool CanIssueElectronicInvoices,
    bool CanUseInventory,
    bool CanUseCashRegister,
    IReadOnlyList<CompanyOperationalReadinessSectionDto> Sections,
    IReadOnlyList<CompanyOperationalReadinessCashRegisterDto> CashRegisters
);
