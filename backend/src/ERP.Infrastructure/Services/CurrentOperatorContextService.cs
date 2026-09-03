using ERP.Application.Common;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Services;

public sealed class CurrentOperatorContextService : ICurrentOperatorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentOperatorContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsOperatorMode =>
        _httpContextAccessor
            .HttpContext?.User.FindFirst("operator_mode")
            ?.Value == "true";

    public Guid? GlobalAdminUserId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User.FindFirst("global_admin_user_id")?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}
