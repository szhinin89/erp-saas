using ERP.Application.Common;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Fase I-2 — lee X-Branch-Id igual que <see cref="CurrentCompanyService"/> lee X-Company-Id.
/// Solo transporte: no valida CompanyUserBranch (ver IBranchAccessGuard, fase posterior).
/// </summary>
public sealed class CurrentBranchService : ICurrentBranch
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentBranchService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid BranchId
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Branch-Id"].FirstOrDefault();

            if (Guid.TryParse(header, out var id) && id != Guid.Empty)
                return id;

            return JobBranchContext.Current;
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false
           || JobBranchContext.Current != Guid.Empty;

    public bool HasBranchContext => BranchId != Guid.Empty;
}
