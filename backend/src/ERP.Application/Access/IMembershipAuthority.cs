using ERP.Domain.Access.Entities;

namespace ERP.Application.Access;

/// <summary>
/// Fuente de verdad de autorización por membresía: consultas de lectura sobre
/// <see cref="CompanyUserMembership"/> para guards, handlers y la futura capa BusinessPartner.
///
/// Diferencia con <see cref="Interfaces.IAccessRepository"/>:
///   IAccessRepository = repositorio completo (CRUD + queries).
///   IMembershipAuthority = vista de autorización de solo lectura — semántica de dominio clara.
/// </summary>
public interface IMembershipAuthority
{
    /// <summary>
    /// Retorna la membresía activa del usuario en la empresa, o null si no existe o está inactiva.
    /// Usar en guards de acceso a datos operativos (ventas, inventario, contabilidad).
    /// </summary>
    Task<CompanyUserMembership?> GetActiveMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// True si el usuario tiene al menos una membresía activa en alguna empresa del suscriptor.
    /// Usar para validar acceso bootstrap antes de emitir session token.
    /// </summary>
    Task<bool> HasActiveMembershipInSubscriberAsync(
        Guid subscriberId,
        Guid userId,
        CancellationToken ct = default);
}
