namespace ERP.Application.Access.DTOs;

/// <summary>
/// Fase I-C — bloqueo real detectado al construir la pantalla administrativa de usuarios
/// empresariales: no existía ningún endpoint que listara CompanyUserMembership (con inactivos,
/// ProfileId/nombre). Esta proyección es de solo
/// lectura y no agrega ninguna regla de negocio nueva: junta CompanyUserMembership +
/// IdentityUser + AccessProfile, todos ya expuestos individualmente por otros endpoints admin.
/// </summary>
public sealed record CompanyUserMembershipAdminDto(
    Guid CompanyUserId,
    Guid IdentityUserId,
    string Username,
    string FullName,
    string? Email,
    string Role,
    bool IsActive,
    Guid? ProfileId,
    string? ProfileName
);
