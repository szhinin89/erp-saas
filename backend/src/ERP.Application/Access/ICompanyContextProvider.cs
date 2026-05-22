namespace ERP.Application.Access;

/// <summary>
/// Única fuente de resolución de contexto multi-tenant (empresa + membresía operativa).
/// Usar en runtime auth, permisos UI y compatibilidad login/provisioning.
/// </summary>
public interface ICompanyContextProvider
{
    /// <summary>Empresa activa por defecto del suscriptor (login, provisioning).</summary>
    Task<Guid?> ResolveDefaultCompanyIdAsync(Guid subscriberId, CancellationToken ct = default);

    Task<int> CountActiveCompaniesAsync(Guid subscriberId, CancellationToken ct = default);

    /// <summary>Contexto operativo del usuario autenticado en el suscriptor activo.</summary>
    Task<OperationalCompanyContext?> ResolveOperationalForCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Contexto operativo explícito por userId (autorización runtime).</summary>
    Task<OperationalCompanyContext?> ResolveOperationalForUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Contexto operativo mínimo para evaluar permisos de perfil.</summary>
public sealed record OperationalCompanyContext(
    Guid CompanyId,
    Guid UserId,
    Guid? ProfileId,
    bool IsActiveMembership);
