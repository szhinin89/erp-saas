using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Items.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Tipos de Ítem",
    $"perm:{InventoryPermissions.ItemsView}",
    "🏷️",
    "/inventory/item-types",
    null,
    36
)]
[ApiController]
[Route("api/v1/item-types")]
[Authorize]
[Produces("application/json")]
public sealed class ItemTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemTypesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los tipos de ítem.
    /// </summary>
    /// <remarks>
    /// Catálogo propio de cada tenant que clasifica los ítems; no controla comportamiento de inventario, venta ni
    /// compra.
    /// </remarks>
    /// <param name="onlyActive">Si es <c>true</c> devuelve solo los tipos activos.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Tipos de ítem.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        CancellationToken ct = default
    ) => this.ToOkOrBadRequest(await _mediator.Send(new GetItemTypesQuery(onlyActive), ct), "OK");

    /// <summary>
    /// Obtiene un tipo de ítem por id.
    /// </summary>
    /// <param name="id">Id de el tipo de ítem.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Tipo de ítem.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetItemTypeByIdQuery(id), ct));

    /// <summary>
    /// Crea un tipo de ítem.
    /// </summary>
    /// <remarks>
    /// Responde 201 con el tipo creado.
    /// </remarks>
    /// <param name="cmd">Datos del tipo de ítem.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Tipo de ítem creado.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<ItemTypeDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemTypeCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    /// <summary>
    /// Actualiza un tipo de ítem.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400.
    /// </remarks>
    /// <param name="id">Id de el tipo de ítem.</param>
    /// <param name="cmd">Datos del tipo de ítem, con su id.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Tipo de ítem actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateItemTypeCommand cmd,
        CancellationToken ct
    )
    {
        if (id != cmd.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
    }

    /// <summary>
    /// Alterna el estado activo de un tipo de ítem.
    /// </summary>
    /// <remarks>
    /// Si el tipo estaba activo lo desactiva y viceversa. Baja lógica: nunca se elimina.
    /// </remarks>
    /// <param name="id">Id de el tipo de ítem.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado del tipo de ítem alternado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ToggleItemTypeCommand(id), ct));
}
