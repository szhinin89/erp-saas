using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Audit.DTOs;
using ERP.Application.Audit.UseCases.GetEntityActivity;
using ERP.Application.Audit.UseCases.GetMyActivity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Auditoría de actividad (ámbito del usuario o de una entidad en el tenant actual).</summary>
/// <remarks>
/// Autorización: <c>Session</c> únicamente; sin <c>perm:*</c> porque los handlers acotan por tenant y usuario.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
public class ActivityController : ControllerBase
{
    private readonly GetMyActivityHandler _getMy;
    private readonly GetEntityActivityHandler _getEntity;

    public ActivityController(GetMyActivityHandler getMy, GetEntityActivityHandler getEntity)
    {
        _getMy = getMy;
        _getEntity = getEntity;
    }

    /// <summary>
    /// Historial del usuario autenticado (últimas acciones).
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] string? module = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _getMy.HandleAsync(module, page, pageSize, ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UserActivityDto>());
    }

    /// <summary>
    /// Últimos movimientos de auditoría sobre una entidad (tenant actual), cualquier usuario que haya actuado.
    /// </summary>
    [HttpGet("entity")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForEntity(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        var result = await _getEntity.HandleAsync(entityType, entityId, take, ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UserActivityDto>());
    }
}

