using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessTokenService
{
    string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> tenantIds);
    string GenerateSessionToken(IdentityUser user, Guid tenantId, string role);
    string GenerateSessionToken(Guid userId, Guid tenantId, string role);
}
