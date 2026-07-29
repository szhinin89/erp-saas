using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

public sealed class OperationalContextService : IOperationalContext
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public OperationalContextService(
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _tenant = tenant;
        _company = company;
        _user = user;
    }

    public Guid TenantId => _tenant.TenantId;
    public Guid CompanyId => _company.CompanyId;
    public Guid UserId => _user.UserId;
    public string Role => _user.Role ?? string.Empty;

    public bool HasTenant => _tenant.TenantId != Guid.Empty;
    public bool HasCompany => _company.HasCompanyContext;
    public bool IsAuthenticated => _user.IsAuthenticated;
}
