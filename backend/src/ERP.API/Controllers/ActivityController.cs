using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Audit.DTOs;
using ERP.Application.Audit.UseCases.GetEntityActivity;
using ERP.Application.Audit.UseCases.GetMyActivity;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Auditoría de actividad (ámbito del usuario o de una entidad en el tenant actual).</summary>
[AppFeature(
    "Actividad",
    $"perm:{AdminPermissions.ActivityView}",
    "📜",
    "/admin/activity",
    null,
    200
)]
[ApiController]
[Route("api/v1/admin/activity")]
[Authorize(Policy = $"perm:{AdminPermissions.ActivityView}")]
[Produces("application/json")]
public class ActivityController : ControllerBase
{
    private readonly IMediator _mediator;

    public ActivityController(IMediator mediator) => _mediator = mediator;

    /// <summary>Historial del usuario autenticado (últimas acciones).</summary>
    [HttpGet("my")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<UserActivityDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetMy(
        [FromQuery] string? module = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetMyActivityQuery(module, page, pageSize),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UserActivityDto>());
    }

    /// <summary>
    /// Últimos movimientos de auditoría sobre una entidad (tenant actual). ADMIN-SESSIONS-
    /// ACTIVITY-POLISH-01: endpoint funcional, sin consumidor en el frontend todavía — reservado
    /// para un futuro "Ver actividad de este registro" en fichas de detalle (usuario, documento,
    /// etc.). No eliminar; no exponer en UI sin ese diseño aprobado (ver activityService.ts).
    /// </summary>
    [HttpGet("entity")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<UserActivityDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetForEntity(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetEntityActivityQuery(entityType, entityId, take),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UserActivityDto>());
    }
}
