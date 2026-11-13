using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ListStockAdjustments;
using ERP.Application.Modules.Inventory.Stock.UseCases.UpdateStockAdjustment;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// ZH-API-THIN-STOCK-01 — ajustes de inventario (agregado StockAdjustment: Draft → Executed →
// Cancelled), separados de StockController sin cambiar contrato: misma ruta base, mismas policies
// (class [Authorize] + perm:inventory.adjustments.*), mismo tag OpenAPI "Stock" y el mismo
// [AppFeature] "Ajustes de inventario" a nivel de acción. Comentario no-XML a propósito: un
// <summary> de clase agregaría un tag nuevo al documento OpenAPI.
[ApiController]
[Route("api/v1/inventory/stock")]
[Authorize]
[Tags("Stock")]
[Produces("application/json")]
public sealed class StockAdjustmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockAdjustmentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los ajustes de inventario con filtros y paginación.
    /// </summary>
    /// <remarks>
    /// Resultado paginado (20 por página por defecto).
    /// </remarks>
    /// <param name="warehouseId">Filtra por bodega.</param>
    /// <param name="status">Filtra por estado del ajuste (Draft, Executed o Cancelled).</param>
    /// <param name="reasonId">Filtra por motivo de ajuste.</param>
    /// <param name="movementType">Filtra por tipo de movimiento del ajuste.</param>
    /// <param name="startDate">Fecha de negocio inicial (<c>YYYY-MM-DD</c>).</param>
    /// <param name="endDate">Fecha de negocio final (<c>YYYY-MM-DD</c>).</param>
    /// <param name="pageNumber">Número de página, base 1.</param>
    /// <param name="pageSize">Cantidad de ajustes por página (20 por defecto).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de ajustes.</response>
    [AppFeature(
        "Ajustes de inventario",
        $"perm:{InventoryPermissions.AdjustmentsView}",
        "tune",
        "/inventory/adjustments",
        null,
        23
    )]
    [HttpGet("adjustments")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PagedResult<StockAdjustmentDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> ListAdjustments(
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? status,
        [FromQuery] Guid? reasonId,
        [FromQuery] string? movementType,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(
            new ListStockAdjustmentsQuery(
                warehouseId,
                status,
                reasonId,
                movementType,
                startDate,
                endDate,
                pageNumber,
                pageSize
            ),
            ct
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene un ajuste de inventario con sus líneas.
    /// </summary>
    /// <param name="id">Id de el ajuste de inventario.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ajuste de inventario.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("adjustments/{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsView}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<StockAdjustmentDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdjustment(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStockAdjustmentByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea un ajuste de inventario en estado Draft.
    /// </summary>
    /// <remarks>
    /// Responde 201. El motivo debe ser de la empresa, estar activo y admitir la dirección del ajuste (ingreso,
    /// egreso o ambos); si exige observaciones, deben venir.
    /// </remarks>
    /// <param name="command">Cabecera y líneas del ajuste.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Ajuste creado.</response>
    [HttpPost("adjustments")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsCreate}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<StockAdjustmentDto>),
        StatusCodes.Status201Created
    )]
    public async Task<IActionResult> CreateAdjustment(
        [FromBody] CreateStockAdjustmentCommand command,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza un ajuste en estado Draft.
    /// </summary>
    /// <remarks>
    /// Actualiza la cabecera y reemplaza todas las líneas. Solo un ajuste en Draft se edita. El id de la ruta debe
    /// coincidir con el <c>id</c> del cuerpo; si no, responde 400 con cuerpo vacío.
    /// </remarks>
    /// <param name="id">Id de el ajuste de inventario.</param>
    /// <param name="command">Cabecera y líneas del ajuste, con su id.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ajuste actualizado.</response>
    /// <response code="400">La operación incumple una regla de negocio (envelope). Si el id de la ruta no coincide con el del cuerpo también responde 400, pero con cuerpo vacío y sin envelope.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("adjustments/{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsUpdate}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<StockAdjustmentDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAdjustment(
        Guid id,
        [FromBody] UpdateStockAdjustmentCommand command,
        CancellationToken ct = default
    )
    {
        if (id != command.Id)
            return BadRequest();
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Ejecuta un ajuste Draft.
    /// </summary>
    /// <remarks>
    /// Aplica los movimientos de stock del ajuste en una transacción con el ajuste bloqueado y pasa el ajuste a
    /// Executed. Solo un ajuste en Draft se ejecuta.
    /// </remarks>
    /// <param name="id">Id de el ajuste de inventario.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ajuste ejecutado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("adjustments/{id:guid}/execute")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsConfirm}")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteAdjustment(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExecuteStockAdjustmentCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Anula un ajuste ejecutado.
    /// </summary>
    /// <remarks>
    /// Solo un ajuste en Executed se anula, con motivo obligatorio. Registra el movimiento contrario por línea en
    /// el Kardex y pasa el ajuste a Cancelled.
    /// </remarks>
    /// <param name="id">Id de el ajuste de inventario.</param>
    /// <param name="request">Motivo de la anulación.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ajuste anulado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("adjustments/{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{InventoryPermissions.AdjustmentsCancel}")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAdjustment(
        Guid id,
        [FromBody] CancelStockAdjustmentRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _mediator.Send(new CancelStockAdjustmentCommand(id, request.Reason), ct);
        return this.ToOkOrBadRequest(result);
    }
}

public sealed record CancelStockAdjustmentRequest(string Reason);
