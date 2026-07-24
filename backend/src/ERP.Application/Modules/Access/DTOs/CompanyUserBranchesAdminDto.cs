namespace ERP.Application.Access.DTOs;

/// <summary>
/// Fase I-B — proyección administrativa de sucursales autorizadas para una
/// CompanyUserMembership. Pensada para que un futuro frontend construya directamente un selector
/// (branchId/branchName/authorized) sin resolver nada adicional. Solo incluye sucursales activas
/// de la empresa de la membresía — una sucursal inactiva nunca es una opción válida para
/// autorizar (mismo criterio que CompanyUserPreferencesDefaultBranchValidation).
/// </summary>
public sealed record CompanyUserBranchesAdminDto(
    Guid CompanyUserId,
    IReadOnlyList<CompanyUserBranchOptionDto> Branches);

public sealed record CompanyUserBranchOptionDto(
    Guid BranchId,
    string BranchName,
    bool Authorized);
