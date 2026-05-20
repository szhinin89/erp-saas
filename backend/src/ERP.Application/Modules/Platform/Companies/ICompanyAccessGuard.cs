using ERP.Application.Common;

namespace ERP.Application.Modules.Platform.Companies;

public sealed record CompanyAccessContext(
    Guid UserId,
    Guid SubscriberId,
    Guid CompanyId,
    string Role,
    bool SubscriberIsActive,
    bool CompanyIsActive);

/// <summary>
/// Central membership + subscriber scope validation. Handlers must not duplicate these checks.
/// </summary>
public interface ICompanyAccessGuard
{
    Task<Result<Guid>> RequireActiveSubscriberAsync(CancellationToken ct = default);

    Task<Result<CompanyAccessContext>> RequireMembershipAsync(
        Guid companyId,
        bool requireActiveCompany = true,
        CancellationToken ct = default);

    Task<Result<CompanyAccessContext>> RequireCurrentCompanyAsync(CancellationToken ct = default);
}
