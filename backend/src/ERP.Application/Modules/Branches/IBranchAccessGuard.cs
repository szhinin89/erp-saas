using ERP.Application.Common;

namespace ERP.Application.Modules.Branches;

public sealed record BranchAccessContext(
    Guid UserId,
    Guid TenantId,
    Guid CompanyId,
    Guid BranchId,
    string BranchName,
    bool IsMainBranch
);

/// <summary>
/// Central branch scope + CompanyUserBranch validation. Handlers must not duplicate these
/// checks — misma filosofía que <c>ICompanyAccessGuard</c>. Reutiliza
/// <c>ICompanyAccessGuard.RequireCurrentCompanyAsync</c> para tenant+empresa+membership en vez
/// de reimplementarlo (fuente única para esa parte de la cadena).
/// </summary>
public interface IBranchAccessGuard
{
    /// <summary>
    /// Valida, en orden: empresa operativa activa (vía ICompanyAccessGuard) → la sucursal
    /// existe → está activa → pertenece a la empresa operativa actual → el usuario tiene una
    /// CompanyUserBranch activa para esa sucursal. Semántica de RECURSO (sucursal de un documento,
    /// sucursal solicitada): NOT_FOUND (inexistente o de otra empresa/tenant, indistinguibles),
    /// FORBIDDEN (existe en la empresa pero está deshabilitada o sin CompanyUserBranch), más los
    /// códigos de contexto de <see cref="ERP.Application.Modules.Companies.ICompanyAccessGuard.RequireCurrentCompanyAsync"/>.
    /// </summary>
    Task<Result<BranchAccessContext>> RequireBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sucursal operativa del request (<c>ICurrentBranch</c>, header X-Branch-Id) — semántica de
    /// CONTEXTO: sin sucursal o cualquier rechazo de la sucursal → BRANCH_SCOPE_FORBIDDEN (el
    /// frontend lo usa para pedir otra sucursal); UNAUTHORIZED y COMPANY_SCOPE_FORBIDDEN se
    /// conservan. Única fuente de ese mapeo — BranchScopeBehavior y los handlers que validan la
    /// sucursal activa por su cuenta la consumen, nunca traducen por su lado.
    /// </summary>
    Task<Result<BranchAccessContext>> RequireCurrentBranchAsync(
        CancellationToken cancellationToken = default
    );
}
