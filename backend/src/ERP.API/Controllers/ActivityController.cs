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

    /// <summary>
    /// Historial de actividad reciente del usuario autenticado.
    /// </summary>
    /// <remarks>
    /// Los valores de paginación fuera de rango se corrigen, no se rechazan: page menor a 1 usa 1;
    /// pageSize menor a 1 usa 25 y mayor a 100 se limita a 100.
    /// </remarks>
    /// <param name="module">Filtra por módulo de origen (opcional).</param>
    /// <param name="page">Número de página, desde 1.</param>
    /// <param name="pageSize">Tamaño de página (25 por defecto, máximo 100).</param>
    /// <response code="200">
    /// Actividad del usuario, de la más reciente a la más antigua (posiblemente vacía).
    /// </response>
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
    /// Últimos movimientos de auditoría de una entidad del tenant actual.
    /// </summary>
    /// <remarks>
    /// entityType admite Branch, Warehouse, BusinessPartner, IdentityUser y CompanyUserMembership;
    /// la entidad debe pertenecer a la empresa activa (los socios de negocio son del tenant). Si el
    /// tipo es desconocido o la entidad está fuera de alcance responde 200 con lista vacía. take
    /// vale 10 por defecto y se corrige al rango 1 a 50. Las acciones de cuentas globales se
    /// muestran con un nombre genérico y sin correo.
    /// </remarks>
    /// <param name="entityType">Tipo de entidad (ver observaciones).</param>
    /// <param name="entityId">Id de la entidad.</param>
    /// <param name="take">Cantidad de movimientos (10 por defecto, máximo 50).</param>
    /// <response code="200">Movimientos de la entidad (posiblemente vacíos).</response>
    /// <response code="400">entityId vacío o entityType vacío.</response>
    /// <response code="403">No hay una empresa activa en la sesión.</response>
    // ADMIN-SESSIONS-ACTIVITY-POLISH-01: endpoint funcional sin consumidor en el frontend todavía,
    // reservado para un futuro "Ver actividad de este registro" en fichas de detalle. No eliminar;
    // no exponer en UI sin ese diseño aprobado (ver activityService.ts).
    [HttpGet("entity")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<UserActivityDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
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
