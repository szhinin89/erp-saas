namespace ERP.Application.Modules.Company.DTOs;

/// <summary>Fila del dashboard AdminGlobalCore: una empresa con los datos mínimos de su tenant.</summary>
public sealed record AdminCoreCompanyDto(
    Guid TenantId,
    string TenantName,
    bool TenantIsActive,
    Guid CompanyId,
    string Ruc,
    string LegalName,
    string? TradeName,
    bool IsActive
);
