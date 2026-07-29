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

    /// <summary>Lista bodegas con filtros de estado, búsqueda y sucursal.</summary>
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

    /// <summary>Devuelve el detalle completo de una bodega por su Id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesView}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetWarehouseByIdQuery(id), cancellationToken);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Crea una nueva bodega en la sucursal indicada.</summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesCreate}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseListItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWarehouseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Actualiza los datos de una bodega existente.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>Desactiva una bodega (soft delete).</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesDelete}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableWarehouseCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva una bodega previamente desactivada.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.WarehousesUpdate}")]
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
