using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessTokenService
{
    string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> subscriberIds);
    string GenerateSessionToken(IdentityUser user, Guid subscriberId, string role, Guid companyId = default);

    /// <summary>JWT de operador platform (SuperAdmin global).</summary>
    string GeneratePlatformSessionToken(IdentityUser platformUser);

    string GenerateBootstrapToken(
        Guid userId,
        string email,
        string fullName,
        string role,
        IReadOnlyList<Guid> subscriberIds,
        IdentityUserType userType = IdentityUserType.Company,
        PlatformRole? platformRole = null);

    string GenerateSessionToken(
        Guid userId,
        string email,
        string fullName,
        Guid subscriberId,
        string role,
        Guid companyId = default,
        IdentityUserType userType = IdentityUserType.Company,
        PlatformRole? platformRole = null);
}

