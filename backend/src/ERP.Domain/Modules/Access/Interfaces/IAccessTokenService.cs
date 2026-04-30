using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessTokenService
{
    string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> tenantIds);
    string GenerateSessionToken(IdentityUser user, Guid tenantId, string role);

    string GenerateBootstrapToken(
        Guid userId,
        string email,
        string fullName,
        string role,
        IReadOnlyList<Guid> tenantIds);

    string GenerateSessionToken(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        string role);
}

