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
