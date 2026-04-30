using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?
                .User.FindFirst("sub")?.Value;

            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? Email
        => _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value
           ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;

    public string? FullName
        => _httpContextAccessor.HttpContext?.User.FindFirst("full_name")?.Value;

    public string? Role
        => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
}
