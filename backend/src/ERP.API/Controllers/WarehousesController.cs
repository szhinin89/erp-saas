using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Application.Modules.Inventory.UseCases.CreateWarehouse;
using ERP.Application.Modules.Inventory.UseCases.DisableWarehouse;
using ERP.Application.Modules.Inventory.UseCases.EnableWarehouse;
using ERP.Application.Modules.Inventory.UseCases.GetWarehouse;
using ERP.Application.Modules.Inventory.UseCases.ListWarehouses;
using ERP.Application.Modules.Inventory.UseCases.UpdateWarehouse;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// GestiÃ³n de bodegas / almacenes del tenant autenticado.
/// Todos los endpoints filtran automÃ¡ticamente por el tenant del JWT.
/// </summary>
[AppFeature("Bodegas", "perm:inventory.warehouses.view", "ðŸ“¦", "/inventory/warehouses", "perm:inventory.products.view", 40)]
[ApiController]
[Route("api/inventory/warehouses")]
[Authorize]
[Produces("application/json")]
public sealed class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarehousesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista bodegas con filtros opcionales.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:inventory.warehouses.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WarehouseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search       = CatalogQueryParameters.ParseSearch(Request.Query);
        Guid? sucursalId = Request.Query.TryGetValue("sucursalId", out var sv)
            && Guid.TryParse(sv, out var sid) ? sid : null;

        var result = await _mediator.Send(new GetWarehousesQuery(activeFilter, search, sucursalId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<WarehouseDto>());
    }

    /// <summary>Retorna una bodega por su ID.</summary>
    /// <param name="id">ID de la bodega (GUID).</param>
    /// <response code="200">Bodega encontrada.</response>
    /// <response code="404">La bodega no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventory.warehouses.view")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetWarehouseByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Crea una nueva bodega en el tenant.</summary>
    /// <response code="201">Bodega creada.</response>
    /// <response code="400">Nombre duplicado en el tenant.</response>
    /// <response code="422">Datos invÃ¡lidos.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventory.warehouses.create")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWarehouseCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Actualiza los datos de una bodega existente.</summary>
    /// <param name="id">ID de la bodega.</param>
    /// <response code="200">Bodega actualizada.</response>
    /// <response code="400">Nombre duplicado u otro error de negocio.</response>
    /// <response code="422">Datos invÃ¡lidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventory.warehouses.update")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateWarehouseCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Deshabilita una bodega (soft delete). No la elimina.</summary>
    /// <response code="200">Bodega deshabilitada.</response>
    /// <response code="400">La bodega ya estÃ¡ deshabilitada o no existe.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventory.warehouses.delete")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableWarehouseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitada");
    }

    /// <summary>Reactiva una bodega previamente deshabilitada.</summary>
    /// <response code="200">Bodega habilitada.</response>
    /// <response code="400">La bodega ya estÃ¡ activa o no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventory.warehouses.update")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableWarehouseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitada");
    }
}
