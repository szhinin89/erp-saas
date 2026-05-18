using MediatR;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Audit.DTOs;
using ERP.Application.Audit.UseCases.GetEntityActivity;
using ERP.Application.Audit.UseCases.GetMyActivity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Auditoría de actividad (ámbito del usuario o de una entidad en el tenant actual).</summary>
[AppFeature("Actividad", "perm:admin.activity.view", "📜", "/admin/activity", null, 200)]
[ApiController]
[Route("api/admin/activity")]
[Authorize(Policy = "perm:admin.activity.view")]
[Produces("application/json")]
public class ActivityController : ControllerBase
{
    private readonly IMediator _mediator;

    public ActivityController(IMediator mediator) => _mediator = mediator;

    /// <summary>Historial del usuario autenticado (últimas acciones).</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] string? module = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMyActivityQuery(module, page, pageSize), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UserActivityDto>());
    }

    /// <summary>Últimos movimientos de auditoría sobre una entidad (tenant actual).</summary>
    [HttpGet("entity")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForEntity(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEntityActivityQuery(entityType, entityId, take), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<UserActivityDto>());
    }
}
