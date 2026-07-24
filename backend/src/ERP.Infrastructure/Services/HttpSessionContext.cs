using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

public sealed class HttpSessionContext : ISessionContext
{
    private readonly ICurrentTenant  _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser    _user;

    public HttpSessionContext(
        ICurrentTenant  tenant,
        ICurrentCompany company,
        ICurrentUser    user)
    {
        _tenant  = tenant;
        _company = company;
        _user    = user;
    }

    public Guid TenantId => _tenant.TenantId;

    public Guid CompanyId => _company.CompanyId;

    public bool HasTenantContext => _tenant.TenantId != Guid.Empty;

    public bool HasCompanyContext => _company.HasCompanyContext;
}
