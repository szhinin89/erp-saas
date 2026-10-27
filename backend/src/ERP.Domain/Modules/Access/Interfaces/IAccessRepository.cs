using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessRepository
{
    Task<int> CountActiveCompanyUsersAsync(CancellationToken cancellationToken = default);

    Task<IdentityUser?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<IdentityUser>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default
    );
    Task<IdentityUser?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    );
    Task<bool> AnyUserWithEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IdentityUser?> GetUserByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    );
    Task<bool> AnyUserWithUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    );
    Task<int> CountIdentityUsersAsync(CancellationToken cancellationToken = default);
    Task AddUserAsync(IdentityUser user, CancellationToken cancellationToken = default);

    Task AddGlobalUserRoleAsync(GlobalUserRole role, CancellationToken cancellationToken = default);

    Task<GlobalUserRole?> GetActiveGlobalUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CompanyUserMembership>> GetActiveCompanyUserMembershipsForUserSystemAsync(
        Guid identityUserId,
        CancellationToken cancellationToken = default
    );

    Task<CompanyUserMembership?> GetCompanyUserMembershipAsync(
        Guid companyId,
        Guid identityUserId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lookup por Id propio de la membresía — necesario porque CompanyUserPreferences (Fase C)
    /// referencia CompanyUserMembershipId como su única clave natural, sin conocer el par
    /// (CompanyId, IdentityUserId) que exige <see cref="GetCompanyUserMembershipAsync"/>.
    /// </summary>
    Task<CompanyUserMembership?> GetCompanyUserMembershipByIdAsync(
        Guid companyUserMembershipId,
        CancellationToken cancellationToken = default
    );

    Task AddCompanyUserMembershipAsync(
        CompanyUserMembership membership,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByCompanyAsync(
        Guid companyId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByTenantAsync(
        Guid tenantId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    );

    Task<int> CountActiveCompanyUserMembershipsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    Task<int> CountActiveCompanyUserMembershipsByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    /// <summary>SEC-05: perfiles de una empresa (tenant + empresa explícitos).</summary>
    Task<IReadOnlyList<AccessProfile>> GetProfilesByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>SEC-05: <c>null</c> si el perfil no pertenece a ese tenant y empresa.</summary>
    Task<AccessProfile?> GetProfileByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid profileId,
        CancellationToken cancellationToken = default
    );
    Task AddProfileAsync(AccessProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permisos guardados de un perfil. Solo para un perfil ya resuelto con
    /// <see cref="GetProfileByIdAsync"/> en el tenant y empresa validados.
    /// </summary>
    Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(
        Guid tenantId,
        Guid profileId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// SEC-05 — resolución efectiva (runtime): permisos del perfil solo si pertenece a ese tenant
    /// y empresa y está activo; si no, vacío. No depende del contexto de empresa del request
    /// (la empresa operativa puede resolverse sin <c>X-Company-Id</c>).
    /// </summary>
    Task<IReadOnlyList<AccessProfilePermission>> GetActiveProfilePermissionsForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid profileId,
        CancellationToken cancellationToken = default
    );
    Task<AccessProfilePermission?> GetProfilePermissionAsync(
        Guid tenantId,
        Guid profileId,
        string permissionKey,
        CancellationToken cancellationToken = default
    );
    Task AddProfilePermissionAsync(
        AccessProfilePermission permission,
        CancellationToken cancellationToken = default
    );

    Task<(int TotalUsers, int ActiveUsers)> CountDistinctIdentityUsersForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<IdentityUser>> GetActiveIdentityUsersForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
