namespace ERP.Application.Auth.DTOs;

public sealed record AccessibleCompanyDto(
    Guid CompanyId,
    Guid TenantId,
    string LegalName,
    string DisplayName,
    string Ruc,
    string Role);
