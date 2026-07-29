namespace ERP.Domain.Exceptions;

/// <summary>
/// Fallo de aislamiento operativo por sucursal (sin contexto, sucursal ajena, sin
/// CompanyUserBranch autorizada). Mismo criterio que <see cref="CompanyScopeException"/>.
/// </summary>
public sealed class BranchScopeException : Exception
{
    public string Code { get; }

    private BranchScopeException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public static BranchScopeException NoBranchContext() =>
        new("branch_context_required", "No hay sucursal operativa seleccionada.");

    public static BranchScopeException AccessDenied(string? detail = null) =>
        new("branch_access_denied", detail ?? "No tiene acceso a esta sucursal.");
}
