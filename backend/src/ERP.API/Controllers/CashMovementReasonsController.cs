using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — catálogo administrable de motivos de movimiento manual de
/// caja (SSOT dinámico), scope obligatorio Tenant+Company. Consumido por el formulario "Registrar
/// movimiento manual de efectivo" en /treasury/cash. No borra físico: Toggle solo activa/desactiva.
/// </summary>
[AppFeature(
    "Motivos de caja",
    $"perm:{CajaPermissions.View}",
    "list_alt",
    "/treasury/cash/reasons",
    null,
    71
)]
[ApiController]
[Route("api/v1/cash-movement-reasons")]
[Authorize]
[Produces("application/json")]
public sealed class CashMovementReasonsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashMovementReasonsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista los motivos de movimiento manual de caja de la empresa activa.</summary>
    /// <param name="movementType">Filtra por tipo: <c>ManualIncome</c>, <c>ManualExpense</c> o <c>Withdrawal</c> (sin distinguir mayúsculas); omitido devuelve todos.</param>
    /// <param name="includeInactive">Si es <c>true</c> incluye los motivos inactivos; por defecto solo activos.</param>
    /// <response code="200">Motivos de caja.</response>
    /// <response code="422">Tipo de movimiento no válido.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CashMovementReasonDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> List(
        [FromQuery] string? movementType = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetCashMovementReasonsQuery(movementType, includeInactive),
                ct
            ),
            "OK",
            () => Array.Empty<CashMovementReasonDto>()
        );

    /// <summary>Crea un motivo de movimiento manual de caja en la empresa activa.</summary>
    /// <remarks>El código debe ser único dentro de la empresa y el tipo debe ser <c>ManualIncome</c>, <c>ManualExpense</c> o <c>Withdrawal</c>. El motivo nace activo y sin cuenta contable (ver <c>PUT {id}/account</c>).</remarks>
    /// <param name="command">Código, nombre, tipo de movimiento y orden de presentación.</param>
    /// <response code="201">Motivo creado.</response>
    /// <response code="409">Ya existe un motivo con ese código en la empresa.</response>
    /// <response code="422">Datos inválidos (código, nombre o tipo de movimiento).</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CashMovementReasonDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCashMovementReasonCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Actualiza nombre y orden de un motivo; código y tipo no cambian.</summary>
    /// <param name="id">Id del motivo; debe coincidir con <c>id</c> del cuerpo.</param>
    /// <param name="command">Nuevo nombre y orden.</param>
    /// <response code="200">Motivo actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo: `ProblemDetails` estándar de ASP.NET Core, sin envelope. Es el único 400 de la operación (las reglas de entrada responden 422).</response>
    /// <response code="404">El motivo no existe en la empresa activa.</response>
    /// <response code="422">Datos inválidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CashMovementReasonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCashMovementReasonCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return BadRequest();
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>Activa o desactiva un motivo (baja lógica; no se elimina).</summary>
    /// <remarks>Alterna el estado actual: un motivo activo pasa a inactivo y viceversa. Un motivo inactivo no puede usarse en nuevos movimientos manuales.</remarks>
    /// <param name="id">Id del motivo.</param>
    /// <response code="200">Motivo con su nuevo estado.</response>
    /// <response code="404">El motivo no existe en la empresa activa.</response>
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CashMovementReasonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ToggleCashMovementReasonCommand(id), ct));

    /// <summary>
    /// ACCOUNTING-12C — cuenta contable del motivo (contrapartida del asiento del movimiento) para la
    /// Company activa; la Company sale del contexto autenticado, nunca del body.
    /// </summary>
    /// <remarks>La cuenta debe pertenecer a la empresa, estar activa y admitir movimientos (imputable).</remarks>
    /// <param name="id">Id del motivo.</param>
    /// <param name="request">Cuenta contable de contrapartida.</param>
    /// <response code="200">Motivo con su cuenta contable asignada.</response>
    /// <response code="404">El motivo no existe en la empresa activa.</response>
    /// <response code="422">La cuenta contable no existe, no es de la empresa, está inactiva o no admite movimientos.</response>
    [HttpPut("{id:guid}/account")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CashMovementReasonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetAccount(
        Guid id,
        [FromBody] SetCashMovementReasonAccountRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SetCashMovementReasonAccountCommand(id, request.AccountingAccountId), ct)
        );
}

public sealed record SetCashMovementReasonAccountRequest(Guid AccountingAccountId);
