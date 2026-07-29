namespace ERP.Application.Access.DTOs;

/// <summary>
/// Proyección administrativa mínima de CompanyUserPreferences para el endpoint de
/// administración (Fase F). Expone únicamente los tres datos que la entidad posee — nunca
/// TenantId/CompanyId/Id interno de la fila, que son detalle de infraestructura sin valor para
/// un admin consultando/editando la preferencia de un usuario.
/// </summary>
public sealed record CompanyUserPreferencesAdminDto(
    Guid CompanyUserId,
    Guid? DefaultBranchId,
    string LoginMode
);
