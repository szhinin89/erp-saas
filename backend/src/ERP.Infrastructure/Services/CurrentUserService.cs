using ERP.Application.Common;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

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
            var claim =
                _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? Username => _httpContextAccessor.HttpContext?.User.FindFirst("username")?.Value;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Nombre visible completo, leído de <c>ClaimTypes.Name</c> (claim vigente, emitida por
    /// <c>AccessTokenService</c>). El fallback a <c>ClaimTypes.GivenName</c> es únicamente de
    /// compatibilidad transitoria con tokens ya emitidos antes de esta corrección — esa claim
    /// representaba erróneamente el nombre completo (semánticamente es "solo el nombre") y deja
    /// de emitirse en tokens nuevos. Retirar el fallback una vez expiren los tokens antiguos
    /// (≤ Jwt:ExpirationMinutes desde el despliegue de este cambio).
    /// </summary>
    public string? FullName =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.GivenName)?.Value;

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
}
