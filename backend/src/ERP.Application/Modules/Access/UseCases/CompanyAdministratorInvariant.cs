using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Kernel.Security;

namespace ERP.Application.Access.UseCases;

/// <summary>
/// Invariante de negocio: toda empresa debe conservar al menos un Administrador activo. Única
/// fuente de verdad — se evalúa sobre el ESTADO FUTURO de una membership (<see cref="FutureState"/>),
/// nunca sobre el mecanismo que lo origina (revocación, cambio de rol, desactivación de usuario,
/// restauración, o cualquier operación administrativa futura que module Role/IsActive). Todo
/// handler que pueda dejar una membership sin ser Admin activo debe consultar esto antes de
/// persistir el cambio — nunca reimplementar el conteo.
///
/// Regla pura: no consulta el repositorio. El caller obtiene primero las memberships activas de
/// la empresa (vía <c>IAccessRepository.GetCompanyUserMembershipsByCompanyAsync</c>, ya existente)
/// y se las entrega — este helper solo evalúa.
/// </summary>
internal static class CompanyAdministratorInvariant
{
    public const string ViolationMessage =
        "La empresa debe conservar al menos un administrador activo.";

    /// <summary>Estado al que quedará la membership tras la operación — no cómo se llega a él.</summary>
    public readonly record struct FutureState(bool IsActive, string Role);

    /// <param name="membership">La membership cuyo estado futuro se está evaluando.</param>
    /// <param name="futureState">El estado (IsActive, Role) que tendrá tras la operación.</param>
    /// <param name="activeCompanyMemberships">
    /// Memberships activas de la empresa de <paramref name="membership"/>, obtenidas por el
    /// caller ANTES de persistir el cambio (por eso <paramref name="membership"/> todavía aparece
    /// en esta colección con su estado actual, no el futuro — se excluye por Id al contar).
    /// </param>
    public static Result<T>? Validate<T>(
        CompanyUserMembership membership,
        FutureState futureState,
        IReadOnlyCollection<CompanyUserMembership> activeCompanyMemberships
    )
    {
        // Si la membership continuará siendo un Administrador activo, el invariante queda
        // satisfecho independientemente del resto de administradores existentes — no hace falta
        // mirar ninguna otra membership.
        if (futureState.IsActive && futureState.Role == SecurityRoles.Admin)
            return null;

        var anotherActiveAdminExists = activeCompanyMemberships.Any(m =>
            m.Id != membership.Id && m.Role == SecurityRoles.Admin
        );

        return anotherActiveAdminExists ? null : Result<T>.ValidationFailure(ViolationMessage);
    }
}
