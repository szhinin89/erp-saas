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

    Task AddGlobalUserRoleAsync(
        GlobalUserRole role,
        CancellationToken cancellationToken = default
    );

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

    Task<IReadOnlyList<AccessProfile>> GetProfilesByTenantAsync(
        Guid tenantId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    );
    Task<AccessProfile?> GetProfileByIdAsync(
        Guid tenantId,
        Guid profileId,
        CancellationToken cancellationToken = default
    );
    Task AddProfileAsync(AccessProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(
        Guid tenantId,
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
