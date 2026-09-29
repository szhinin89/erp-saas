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
/// Cada fallo lleva código canónico y los consumidores lo PROPAGAN (nunca deciden por el texto):
/// <list type="bullet">
/// <item>UNAUTHORIZED — sin sesión.</item>
/// <item>COMPANY_SCOPE_FORBIDDEN — contexto inválido: tenant ausente/inactivo, y cualquier rechazo
/// de <see cref="RequireCurrentCompanyAsync"/> (sin empresa operativa, empresa ajena, no operativa,
/// sin membership).</item>
/// <item><see cref="RequireMembershipAsync"/> (empresa pedida por id, semántica de recurso):
/// NOT_FOUND (inexistente o de otro tenant, indistinguibles) y FORBIDDEN (existe en el tenant
/// pero no está operativa o no hay membership).</item>
/// </list>
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
