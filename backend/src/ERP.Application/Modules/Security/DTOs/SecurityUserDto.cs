namespace ERP.Application.Security.DTOs;

/// <summary>
/// <see cref="CompanyUserMembershipId"/> agregado en Fase G — el frontend lo necesita para poder
/// invocar el endpoint administrativo de CompanyUserPreferences (Fase F), que opera sobre
/// CompanyUserMembershipId y no sobre IdentityUser.Id (<see cref="Id"/> de este DTO). Bloqueo
/// real: esta era la única pantalla administrativa existente que lista CompanyUser, y no exponía
/// el Id de membresía necesario.
/// </summary>
public record SecurityUserDto(
    Guid Id,
    string Username,
    string FullName,
    string? Email,
    string Role,
    bool IsActive,
    Guid CompanyUserMembershipId
);
