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
[AppFeature(
    "Sesiones (Admin)",
    $"perm:{AccessPermissions.SessionsView}",
    "🖥️",
    "/admin/access/sessions",
    null,
    211
)]
[ApiController]
[Route("api/v1/admin/access/sessions")]
[Authorize(Policy = $"perm:{AccessPermissions.SessionsView}")]
[Produces("application/json")]
public sealed class AdminUserSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUserSessionController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista paginada de las sesiones de la empresa activa.
    /// </summary>
    /// <remarks>
    /// El alcance es siempre la empresa activa del llamante (companyId no amplía el alcance a otras
    /// empresas) y excluye las sesiones de cuentas globales. status acepta Active, ClosedManually,
    /// ClosedByNewLogin o Expired, sin distinguir mayúsculas.
    /// </remarks>
    /// <param name="identityUserId">Filtra por usuario (opcional).</param>
    /// <param name="companyId">Sin efecto sobre el alcance: siempre rige la empresa activa.</param>
    /// <param name="status">Estado de la sesión (opcional).</param>
    /// <param name="fromUtc">Inicio del rango de inicio de sesión, instante UTC (opcional).</param>
    /// <param name="toUtc">Fin del rango, instante UTC (opcional).</param>
    /// <param name="pageNumber">Número de página, desde 1.</param>
    /// <param name="pageSize">Tamaño de página, de 1 a 200 (25 por defecto).</param>
    /// <response code="200">Página de sesiones.</response>
    /// <response code="403">No hay una empresa activa en la sesión.</response>
    /// <response code="422">
    /// pageNumber menor a 1, pageSize fuera de 1 a 200, status inválido o fromUtc posterior a
    /// toUtc.
    /// </response>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<UserSessionAdminDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? identityUserId,
        [FromQuery] Guid? companyId,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var query = new GetUserSessionsPagedQuery(
            identityUserId,
            companyId,
            status,
            fromUtc,
            toUtc,
            pageNumber,
            pageSize
        );

        var result = await _mediator.Send(query, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cuenta las sesiones de la empresa activa por estado.
    /// </summary>
    /// <remarks>
    /// Mismo alcance que el listado: empresa activa, sin cuentas globales. El parámetro companyId
    /// se ignora.
    /// </remarks>
    /// <param name="companyId">Ignorado: siempre rige la empresa activa.</param>
    /// <response code="200">Totales por estado de sesión.</response>
    /// <response code="403">No hay una empresa activa en la sesión.</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<SessionStatisticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] Guid? companyId,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new GetSessionStatisticsQuery(companyId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Cierra manualmente una sesión de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Marca la sesión como cerrada manualmente. Una sesión de otra empresa o de una cuenta global
    /// responde como inexistente (404). Es idempotente: cerrar una sesión que ya no está activa
    /// responde 200 sin cambios.
    /// </remarks>
    /// <param name="id">Id de la sesión.</param>
    /// <response code="200">Sesión cerrada.</response>
    /// <response code="404">
    /// La sesión no existe, es de otra empresa o pertenece a una cuenta global.
    /// </response>
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
