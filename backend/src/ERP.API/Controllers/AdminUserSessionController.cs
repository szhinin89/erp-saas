using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CloseUserSessionAdmin;
using ERP.Application.Access.UseCases.GetSessionStatistics;
using ERP.Application.Access.UseCases.GetUserSessionsPaged;
using ERP.Application.Common;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Dashboard administrativo de sesiones (Fase 10) — operativo/de soporte, NO reemplaza ni
/// modifica el flujo de autenticación (Login/SwitchCompany/Logout siguen en AuthController /
/// UserSessionController). Controller separado a propósito: sigue la misma convención ya
/// establecida por ActivityController/AccessSessionController/AccessProfilesController
/// (áreas administrativas bajo api/v1/admin/*, con su propio permiso IAM).
/// </summary>
[AppFeature("Sesiones (Admin)", $"perm:{AccessPermissions.SessionsView}", "🖥️", "/admin/access/sessions", null, 211)]
[ApiController]
[Route("api/v1/admin/access/sessions")]
[Authorize(Policy = $"perm:{AccessPermissions.SessionsView}")]
[Produces("application/json")]
public sealed class AdminUserSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUserSessionController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserSessionAdminDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? identityUserId,
        [FromQuery] Guid? companyId,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserSessionsPagedQuery(
            identityUserId, companyId, status, fromUtc, toUtc, pageNumber, pageSize);

        var result = await _mediator.Send(query, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<SessionStatisticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] Guid? companyId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSessionStatisticsQuery(companyId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Requiere access.sessions.close (más restrictivo que la policy del controller, que solo exige access.sessions.view).</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = $"perm:{AccessPermissions.SessionsClose}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CloseUserSessionAdminCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
