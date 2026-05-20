using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessTokenService
{
    string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> subscriberIds);
    string GenerateSessionToken(IdentityUser user, Guid subscriberId, string role, Guid companyId = default);

    string GenerateBootstrapToken(
        Guid userId,
        string email,
        string fullName,
        string role,
        IReadOnlyList<Guid> subscriberIds);

    string GenerateSessionToken(
        Guid userId,
        string email,
        string fullName,
        Guid subscriberId,
        string role,
        Guid companyId = default);
}

