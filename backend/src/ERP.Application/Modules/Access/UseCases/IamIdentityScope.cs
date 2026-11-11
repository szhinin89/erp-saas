using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases;

/// <summary>
/// IAM-V1-F1 — única fuente de verdad del alcance de identidades en la administración
/// empresarial (Usuarios, sesiones, seguridad administrativa, actividad). Mismo patrón que
/// <see cref="CompanyAdministratorInvariant"/>: reglas sin estado; el caller entrega el
/// repositorio y el contexto (tenant/empresa) ya resuelto por los mecanismos existentes
/// (<c>ICurrentTenant</c>, <c>ICurrentCompany</c>, policies <c>perm:*</c>).
///
/// Reglas:
///   - Cuenta global = <see cref="GlobalUserRole"/> activo. Nunca por username, email o perfil.
///   - Las cuentas globales son invisibles y no administrables desde cualquier sesión de
///     empresa, incluido el modo operador (se administran solo desde Admin Core). Esto no toca
///     su membership ni su autorización operativa (<c>RuntimePermissionAuthorizer</c>).
///   - Administrable: membership en la empresa operativa y no global.
///   - Vinculable (agregar usuario existente): membership en alguna empresa del mismo tenant y
///     no global. Una identidad de otro tenant nunca es vinculable.
///   - Fuera de alcance responde exactamente igual que "no existe" en cada endpoint, para no
///     revelar existencia de cuentas globales ni de otros tenants.
/// </summary>
internal static class IamIdentityScope
{
    /// <summary>Respuesta uniforme para identidades inexistentes o fuera de alcance.</summary>
    public const string UserNotFoundMessage = "Usuario no encontrado.";

    public static async Task<bool> IsGlobalAsync(
        IAccessRepository repository,
        Guid identityUserId,
        CancellationToken cancellationToken
    )
    {
        var ids = await repository.GetActiveGlobalUserIdsAsync([identityUserId], cancellationToken);
        return ids?.Contains(identityUserId) == true;
    }

    public static async Task<HashSet<Guid>> GetGlobalUserIdsAsync(
        IAccessRepository repository,
        IEnumerable<Guid> identityUserIds,
        CancellationToken cancellationToken
    )
    {
        var distinct = identityUserIds.Distinct().ToList();
        if (distinct.Count == 0)
            return [];
        var ids = await repository.GetActiveGlobalUserIdsAsync(distinct, cancellationToken);
        return ids is null ? [] : [.. ids];
    }

    /// <summary>
    /// Membership de <paramref name="identityUserId"/> en <paramref name="companyId"/> si la
    /// identidad es administrable desde esa empresa; <c>null</c> si no existe o es global.
    /// </summary>
    public static async Task<CompanyUserMembership?> GetAdministrableMembershipAsync(
        IAccessRepository repository,
        Guid companyId,
        Guid identityUserId,
        CancellationToken cancellationToken
    )
    {
        var membership = await repository.GetCompanyUserMembershipAsync(
            companyId,
            identityUserId,
            cancellationToken
        );
        if (membership is null)
            return null;
        return await IsGlobalAsync(repository, identityUserId, cancellationToken)
            ? null
            : membership;
    }

    /// <summary>
    /// True si la membership (ya resuelta por Id) pertenece a la empresa operativa y no a una
    /// cuenta global.
    /// </summary>
    public static async Task<bool> IsAdministrableMembershipAsync(
        IAccessRepository repository,
        CompanyUserMembership? membership,
        Guid companyId,
        CancellationToken cancellationToken
    ) =>
        membership is not null
        && membership.CompanyId == companyId
        && !await IsGlobalAsync(repository, membership.IdentityUserId, cancellationToken);

    /// <summary>
    /// Identidad visible desde la empresa operativa: administrable en ella, o vinculable desde
    /// otra empresa del mismo tenant. <c>null</c> si no existe, es global o es de otro tenant.
    /// </summary>
    public static async Task<IdentityUser?> FindVisibleUserByUsernameAsync(
        IAccessRepository repository,
        Guid tenantId,
        Guid companyId,
        string username,
        CancellationToken cancellationToken
    )
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await repository.GetUserByUsernameAsync(normalized, cancellationToken);
        if (user is null)
            return null;
        if (await IsGlobalAsync(repository, user.Id, cancellationToken))
            return null;

        var membership = await repository.GetCompanyUserMembershipAsync(
            companyId,
            user.Id,
            cancellationToken
        );
        if (membership is not null)
            return user;

        return await repository.HasCompanyUserMembershipInTenantAsync(
            user.Id,
            tenantId,
            cancellationToken
        )
            ? user
            : null;
    }
}
