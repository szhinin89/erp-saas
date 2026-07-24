using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

public class CurrentTenantService : ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst("tenant_id")?.Value;

            if (Guid.TryParse(claim, out var id))
                return id;

            return JobTenantContext.Current;
        }
    }

    public string? Slug
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst("tenant_slug")?.Value;
            return string.IsNullOrWhiteSpace(claim) ? null : claim;
        }
    }
}
