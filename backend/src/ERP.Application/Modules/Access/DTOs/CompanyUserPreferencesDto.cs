namespace ERP.Application.Access.DTOs;

/// <summary>
/// Proyección de solo lectura de <c>CompanyUserPreferences</c> — nunca se expone la entidad de
/// dominio hacia API. No incluye autorizaciones de sucursal (eso vive exclusivamente en
/// CompanyUserBranch/CompanyUserBranchDto, ajeno a este DTO).
/// </summary>
public sealed record CompanyUserPreferencesDto(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid CompanyUserMembershipId,
    Guid? DefaultBranchId,
    string LoginMode);
