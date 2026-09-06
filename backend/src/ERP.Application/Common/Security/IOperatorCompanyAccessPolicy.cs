namespace ERP.Application.Common.Security;

/// <summary>
/// ERP-CORE-GLOBAL-ADMIN-BRANCH-ACCESS-01 — AdminGlobalCore: única fuente de verdad para decidir
/// si la sesión operativa actual (un admin global que entró a operar una empresa vía
/// <c>POST /auth/global/operate-company</c>) puede sustituir el requisito normal de
/// CompanyUserMembership/CompanyUserBranch. Reutilizada por <c>ICompanyAccessGuard</c> y
/// <c>IBranchAccessGuard</c> (y por las lecturas equivalentes de GetSessionContext/
/// GetMyAvailableBranches) — no duplicar esta verificación en cada consumidor.
///
/// Requiere, revalidado en cada request (nunca cacheado desde el login):
///   1. Claims <c>operator_mode=true</c> + <c>global_admin_user_id</c> en el JWT actual
///      (<see cref="ICurrentOperatorContext"/>), y que ese id coincida con el <c>sub</c> del
///      token (<see cref="ICurrentUser.UserId"/>) — nunca se confía en el claim aislado.
///   2. Una fila <c>GlobalUserRole</c> activa para ese usuario + <c>SecurityRoles.Admin</c> —
///      igual que <c>RequireMembershipAsync</c> revalida <c>CompanyUserMembership.IsActive</c>
///      en cada llamada (nunca solo al emitir el token), por si el rol global fue revocado
///      después de operar la empresa.
///
/// No decide nada sobre la empresa/sucursal en sí — el tenant/company/branch ya vienen acotados
/// por el token de operación (emitido para una empresa específica) y por los propios chequeos de
/// cada guard; esta política solo responde "¿es un operador global autorizado ahora mismo?".
/// Un usuario normal (sin estos claims) siempre obtiene <c>false</c> y sigue requiriendo
/// CompanyUserMembership/CompanyUserBranch explícitos, sin excepción.
/// </summary>
public interface IOperatorCompanyAccessPolicy
{
    Task<bool> IsAuthorizedOperatorAsync(CancellationToken cancellationToken = default);
}
