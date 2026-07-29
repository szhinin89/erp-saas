namespace ERP.Domain.Exceptions;

/// <summary>
/// Fallo de aislamiento operativo por empresa (membership, JWT, empresa ajena).
/// </summary>
public sealed class CompanyScopeException : Exception
{
    public string Code { get; }

    private CompanyScopeException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public static CompanyScopeException NoCompanyContext() =>
        new("company_context_required", "No hay empresa operativa seleccionada.");

    public static CompanyScopeException AccessDenied(string? detail = null) =>
        new("company_access_denied", detail ?? "No tiene acceso a esta empresa.");

    public static CompanyScopeException JwtMismatch() =>
        new("company_jwt_mismatch", "La empresa solicitada no coincide con el contexto de sesión.");

    public static CompanyScopeException TenantInactive() =>
        new("tenant_inactive", "El suscriptor no está activo.");
}
