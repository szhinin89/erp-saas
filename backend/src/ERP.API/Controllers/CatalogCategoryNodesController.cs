using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.CategoryNodes;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// Split out of CatalogController (B-controller max-lines) — mismo Route base, mismos
// endpoints, mismos permisos. Ver CatalogController.cs para el resto del catálogo.
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogCategoryNodesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogCategoryNodesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // CATEGORY NODES (unified tree)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Devuelve el árbol de categorías de ítems.
    /// </summary>
    /// <remarks>
    /// Las categorías forman un árbol propio de cada tenant. Por defecto solo incluye nodos activos.
    /// </remarks>
    /// <param name="includeInactive">Si es <c>true</c> incluye también los nodos inactivos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Árbol de categorías.</response>
    [HttpGet("category-nodes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoryTree(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCategoryTreeQuery(includeInactive), cancellationToken)
        );

    /// <summary>
    /// Obtiene un nodo de categoría por id.
    /// </summary>
    /// <param name="id">Id de el nodo de categoría.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo de categoría.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("category-nodes/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryNodeById(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetCategoryNodeByIdQuery(id), cancellationToken)
        );

    /// <summary>
    /// Crea un nodo de categoría.
    /// </summary>
    /// <remarks>
    /// Responde 201 con el nodo creado. La profundidad máxima del árbol se resuelve por empresa.
    /// </remarks>
    /// <param name="command">Datos del nodo, incluido su nodo padre si lo tiene.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Nodo creado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("category-nodes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryNodeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategoryNode(
        [FromBody] CreateCategoryNodeCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Actualiza un nodo de categoría.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400.
    /// </remarks>
    /// <param name="id">Id de el nodo de categoría.</param>
    /// <param name="command">Datos del nodo, con su id.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPut("category-nodes/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategoryNode(
        Guid id,
        [FromBody] UpdateCategoryNodeCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Desactiva un nodo de categoría.
    /// </summary>
    /// <remarks>
    /// Baja lógica. Un ítem solo puede asignarse a una categoría hoja, activa y que no pertenezca a una rama
    /// deshabilitada.
    /// </remarks>
    /// <param name="id">Id de el nodo de categoría.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo desactivado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("category-nodes/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableCategoryNode(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableCategoryNodeCommand(id), cancellationToken)
        );

    /// <summary>
    /// Activa un nodo de categoría.
    /// </summary>
    /// <param name="id">Id de el nodo de categoría.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo activado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("category-nodes/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableCategoryNode(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableCategoryNodeCommand(id), cancellationToken)
        );
}
