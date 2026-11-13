using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.CreateWarehouse;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.DisableWarehouse;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.EnableWarehouse;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouseById;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.UpdateWarehouse;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Bodegas",
    $"perm:{InventoryPermissions.WarehousesView}",
    "warehouse",
    "/inventory/warehouses",
    null,
    20
)]
[ApiController]
[Route("api/v1/inventory/warehouses")]
[Authorize]
[Produces("application/json")]
public sealed class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarehousesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista las bodegas con filtros de estado, búsqueda y sucursal.
    /// </summary>
    /// <remarks>
    /// Los filtros se leen directamente de la cadena de consulta y no figuran como parámetros del contrato:
    /// <c>activeStatus</c> (<c>all</c>, <c>active</c> o <c>inactive</c>; por defecto solo activas),
    /// <c>onlyActive</c> (compatibilidad: <c>true</c> = activas, <c>false</c> = todas), <c>search</c> (texto libre)
    /// y <c>sucursalId</c> (id de sucursal).
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Bodegas.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<WarehouseListItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var branchId = ParseBranchId(Request.Query);

        var result = await _mediator.Send(
            new GetWarehousesQuery(activeFilter, search, branchId),
            cancellationToken
        );

        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<WarehouseListItemDto>());
    }

    /// <summary>
    /// Devuelve el detalle de una bodega.
    /// </summary>
    /// <param name="id">Id de la bodega.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle de la bodega.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesView}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetWarehouseByIdQuery(id), cancellationToken);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>
    /// Crea una bodega en la sucursal indicada.
    /// </summary>
    /// <remarks>
    /// Responde 201.
    /// </remarks>
    /// <param name="command">Datos de la bodega.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Bodega creada.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesCreate}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseListItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWarehouseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza una bodega.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo. La sucursal solo cambia si la bodega no es la
    /// principal de su sucursal y no tiene existencias distintas de cero ni movimientos de Kardex.
    /// </remarks>
    /// <param name="id">Id de la bodega.</param>
    /// <param name="command">Datos de la bodega, con su id.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Bodega actualizada.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWarehouseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva una bodega.
    /// </summary>
    /// <remarks>
    /// Baja lógica: la bodega no se elimina.
    /// </remarks>
    /// <param name="id">Id de la bodega.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Bodega desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesDelete}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableWarehouseCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reactiva una bodega desactivada.
    /// </summary>
    /// <param name="id">Id de la bodega.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Bodega activada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableWarehouseCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    private static Guid? ParseBranchId(IQueryCollection query)
    {
        if (
            query.TryGetValue("sucursalId", out var raw)
            && Guid.TryParse(raw.ToString(), out var id)
        )
            return id;
        return null;
    }
}
