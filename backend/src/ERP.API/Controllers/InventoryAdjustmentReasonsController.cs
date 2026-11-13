using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ListInventoryAdjustmentReasons;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.SetInventoryAdjustmentReasonAccount;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ToggleInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.UpdateInventoryAdjustmentReason;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo administrable de motivos de ajuste de inventario (INVENTORY-ADJUSTMENTS-02) — SSOT
/// dinámico consumido por CreateStockAdjustment/ExecuteStockAdjustment. No borra físico: Toggle
/// solo activa/desactiva (MasterEntity.Enable/Disable).
/// </summary>
[AppFeature(
    "Motivos de ajuste",
    $"perm:{InventoryPermissions.AdjustmentReasonsView}",
    "list_alt",
    "/inventory/adjustment-reasons",
    null,
    24
)]
[ApiController]
[Route("api/v1/inventory/adjustment-reasons")]
[Authorize]
[Produces("application/json")]
public sealed class InventoryAdjustmentReasonsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryAdjustmentReasonsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los motivos de ajuste de inventario.
    /// </summary>
    /// <remarks>
    /// Si no hay resultados devuelve una lista vacía.
    /// </remarks>
    /// <param name="companyId">Id de empresa por el que se filtra.</param>
    /// <param name="includeInactive">Si es <c>true</c> incluye los motivos inactivos.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Motivos de ajuste.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<InventoryAdjustmentReasonDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new ListInventoryAdjustmentReasonsQuery(companyId, includeInactive),
            ct
        );
        return this.ToOkOrBadRequest(
            result,
            "OK",
            () => Array.Empty<InventoryAdjustmentReasonDto>()
        );
    }

    /// <summary>
    /// Crea un motivo de ajuste de inventario.
    /// </summary>
    /// <remarks>
    /// Responde 201.
    /// </remarks>
    /// <param name="command">Datos del motivo.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Motivo creado.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateInventoryAdjustmentReasonCommand command,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza un motivo de ajuste de inventario.
    /// </summary>
    /// <remarks>
    /// El código del motivo es inmutable. El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no,
    /// responde 400 con cuerpo vacío.
    /// </remarks>
    /// <param name="id">Id de el motivo de ajuste.</param>
    /// <param name="command">Datos del motivo, con su id.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Motivo actualizado.</response>
    /// <response code="400">La operación incumple una regla de negocio (envelope). Si el id de la ruta no coincide con el del cuerpo también responde 400, pero con cuerpo vacío y sin envelope.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateInventoryAdjustmentReasonCommand command,
        CancellationToken ct = default
    )
    {
        if (id != command.Id)
            return BadRequest();
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Activa o desactiva un motivo de ajuste.
    /// </summary>
    /// <remarks>
    /// Baja lógica: nunca elimina el registro.
    /// </remarks>
    /// <param name="id">Id de el motivo de ajuste.</param>
    /// <param name="request">Indica si el motivo se activa (<c>true</c>) o se desactiva (<c>false</c>).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado del motivo actualizado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(
        Guid id,
        [FromBody] ToggleInventoryAdjustmentReasonRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new ToggleInventoryAdjustmentReasonCommand(id, request.Activate),
            ct
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Configura la cuenta contable de un motivo.
    /// </summary>
    /// <remarks>
    /// Define la contrapartida del asiento del ajuste para la empresa activa. La empresa sale del contexto
    /// autenticado, nunca del cuerpo.
    /// </remarks>
    /// <param name="id">Id de el motivo de ajuste.</param>
    /// <param name="request">Id de la cuenta contable.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Cuenta contable del motivo actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/account")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentReasonsManage}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<InventoryAdjustmentReasonDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAccount(
        Guid id,
        [FromBody] SetInventoryAdjustmentReasonAccountRequest request,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SetInventoryAdjustmentReasonAccountCommand(id, request.AccountingAccountId), ct)
        );
}

public sealed record ToggleInventoryAdjustmentReasonRequest(bool Activate);

public sealed record SetInventoryAdjustmentReasonAccountRequest(Guid AccountingAccountId);
