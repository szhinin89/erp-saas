using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Lectura de configuración jerárquica del suscriptor activo (JWT).
/// Escritura administrativa cross-tenant: <see cref="SuperAdminConfigController"/>.
/// </summary>
[ApiController]
[Route("api/settings/config")]
[Authorize]
[Produces("application/json")]
public sealed class SubscriberConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly ICurrentSubscriber _currentSubscriber;

    public SubscriberConfigController(IConfigService configService, ICurrentSubscriber currentSubscriber)
    {
        _configService = configService;
        _currentSubscriber = currentSubscriber;
    }

    /// <summary>Config global del suscriptor en contexto de sesión (Admin o SuperAdmin impersonando).</summary>
    [HttpGet("global")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListGlobal(CancellationToken ct = default)
    {
        if (!_currentSubscriber.IsAuthenticated || _currentSubscriber.SubscriberId == Guid.Empty)
            return this.ApiBadRequest("Contexto de suscriptor no establecido.");

        var rows = await _configService.ListGlobalAsync(_currentSubscriber.SubscriberId, ct);
        return this.ApiOk(rows);
    }
}
