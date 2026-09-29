using ERP.Application.Common;

namespace ERP.Application.Modules.Companies;

public sealed record CompanyAccessContext(
    Guid UserId,
    Guid TenantId,
    Guid CompanyId,
    string Role,
    bool TenantIsActive,
    bool CompanyIsActive
);

/// <summary>
/// Central membership + tenant scope validation. Handlers must not duplicate these checks.
/// Cada fallo lleva código canónico: UNAUTHORIZED (sin sesión), NOT_FOUND (empresa inexistente o
/// de otro tenant — indistinguibles, sin existence leakage), COMPANY_SCOPE_FORBIDDEN (tenant
/// inactivo, sin empresa operativa, empresa no operativa, sin membership). Los consumidores
/// deciden su propio contrato (CompanyScopeBehavior → excepción 403; GetCompanyById → 404 sin
/// código a propósito), así que re-envuelven el mensaje en vez de propagar el código.
/// </summary>
public interface ICompanyAccessGuard
{
    Task<Result<Guid>> RequireActiveTenantAsync(CancellationToken cancellationToken = default);

    Task<Result<CompanyAccessContext>> RequireMembershipAsync(
        Guid companyId,
        bool requireActiveCompany = true,
        CancellationToken cancellationToken = default
    );

    Task<Result<CompanyAccessContext>> RequireCurrentCompanyAsync(
        CancellationToken cancellationToken = default
    );
}
