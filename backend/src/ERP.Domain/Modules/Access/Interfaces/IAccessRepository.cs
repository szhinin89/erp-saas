using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessRepository
{
    Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<IdentityUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnyUserWithEmailAsync(string email, CancellationToken ct = default);
    Task<int> CountIdentityUsersAsync(CancellationToken ct = default);
    Task AddUserAsync(IdentityUser user, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyUserMembership>> GetActiveCompanyUserMembershipsForUserSystemAsync(
        Guid identityUserId, CancellationToken ct = default);

    Task<CompanyUserMembership?> GetCompanyUserMembershipAsync(
        Guid companyId, Guid identityUserId, CancellationToken ct = default);

    Task AddCompanyUserMembershipAsync(CompanyUserMembership membership, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByCompanyAsync(
        Guid companyId, bool onlyActive = true, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsBySubscriberAsync(
        Guid subscriberId, bool onlyActive = true, CancellationToken ct = default);

    Task<int> CountActiveCompanyUserMembershipsByCompanyAsync(Guid companyId, CancellationToken ct = default);

    Task<int> CountActiveCompanyUserMembershipsBySubscriberAsync(Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<AccessProfile>> GetProfilesByTenantAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default);
    Task<AccessProfile?> GetProfileByIdAsync(Guid subscriberId, Guid profileId, CancellationToken ct = default);
    Task AddProfileAsync(AccessProfile profile, CancellationToken ct = default);

    Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(Guid subscriberId, Guid profileId, CancellationToken ct = default);
    Task<AccessProfilePermission?> GetProfilePermissionAsync(Guid subscriberId, Guid profileId, string permissionKey, CancellationToken ct = default);
    Task AddProfilePermissionAsync(AccessProfilePermission permission, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
