using System.Security.Claims;
using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

public interface IAccessTokenService
{
    string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> tenantIds);
    string GenerateSessionToken(IdentityUser user, Guid tenantId, string role);
    string GenerateSessionToken(Guid userId, Guid tenantId, string role);

    /// <summary>
    /// AdminGlobalCore (operate-company): igual que <see cref="GenerateSessionToken(IdentityUser, Guid, string)"/>
    /// pero agrega claims adicionales (p. ej. <c>operator_mode</c>/<c>global_admin_user_id</c>) al
    /// token operativo emitido, para poder auditar/deshacer la operación (ver return-to-global).
    /// </summary>
    string GenerateSessionToken(
        IdentityUser user,
        Guid tenantId,
        string role,
        IEnumerable<Claim> extraClaims
    );
}
