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
    /// CompanyUserBranch activa para esa sucursal.
    /// </summary>
    Task<Result<BranchAccessContext>> RequireBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default
    );
}
