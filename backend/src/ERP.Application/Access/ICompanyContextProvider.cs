using ERP.Domain.Kernel.Security;

namespace ERP.Application.Access;

/// <summary>
/// Única fuente de resolución de contexto multi-tenant (empresa + membresía operativa).
/// Usar en runtime auth, permisos UI y compatibilidad login/provisioning.
/// </summary>
public interface ICompanyContextProvider
{
    /// <summary>Empresa activa por defecto del suscriptor (login, provisioning).</summary>
    Task<Guid?> ResolveDefaultCompanyIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    Task<int> CountActiveCompaniesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Contexto operativo del usuario autenticado en el suscriptor activo.</summary>
    Task<OperationalCompanyContext?> ResolveOperationalForCurrentUserAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Contexto operativo explícito por userId (autorización runtime).</summary>
    Task<OperationalCompanyContext?> ResolveOperationalForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Contexto operativo mínimo para evaluar permisos de perfil. SEC-04: el rol que cuenta es el de
/// la membership en <see cref="CompanyId"/>, nunca el rol del JWT (emitido para la empresa de
/// apertura de sesión). Única excepción: el administrador global en modo operador
/// (<see cref="IsAuthorizedOperator"/>, resuelto por <c>IOperatorCompanyAccessPolicy</c>).
/// </summary>
public sealed record OperationalCompanyContext(
    Guid CompanyId,
    Guid UserId,
    Guid? ProfileId,
    bool IsActiveMembership,
    string? MembershipRole = null,
    bool IsAuthorizedOperator = false
)
{
    /// <summary>Bypass total de permisos (<c>*</c>): Admin de ESTA empresa u operador global.</summary>
    public bool HasAdminBypass =>
        IsAuthorizedOperator
        || (
            IsActiveMembership
            && string.Equals(
                MembershipRole,
                SecurityRoles.Admin,
                StringComparison.OrdinalIgnoreCase
            )
        );

    /// <summary>
    /// Rol efectivo para UI (roles de sesión, visibilidad de navegación). Sin contexto operativo
    /// resuelto se conserva el rol del JWT, salvo Admin: nunca vale sin empresa que lo respalde.
    /// </summary>
    public static string ResolveEffectiveRole(OperationalCompanyContext? context, string? jwtRole)
    {
        if (context is not null && context.HasAdminBypass)
            return SecurityRoles.Admin;
        if (
            context is not null
            && context.IsActiveMembership
            && !string.IsNullOrEmpty(context.MembershipRole)
        )
            return context.MembershipRole;
        return string.Equals(jwtRole, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? SecurityRoles.User
            : jwtRole ?? string.Empty;
    }
}
