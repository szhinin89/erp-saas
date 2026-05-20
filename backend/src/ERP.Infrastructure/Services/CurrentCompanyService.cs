using ERP.Application.Common;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Services;

public sealed class CurrentCompanyService : ICurrentCompany
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentCompanyService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid CompanyId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst("company_id")?.Value;

            if (Guid.TryParse(claim, out var id) && id != Guid.Empty)
                return id;

            return JobCompanyContext.Current;
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false
           || JobCompanyContext.Current != Guid.Empty;

    public bool HasCompanyContext => CompanyId != Guid.Empty;
}
