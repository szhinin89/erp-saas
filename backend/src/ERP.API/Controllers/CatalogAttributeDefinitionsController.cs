using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.AttributeDefinitions;
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
public sealed class CatalogAttributeDefinitionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogAttributeDefinitionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE DEFINITIONS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las definiciones de atributo.
    /// </summary>
    /// <remarks>
    /// Cada definición describe un atributo que puede usarse en las variantes de un ítem.
    /// </remarks>
    /// <param name="groupId">Filtra por grupo de atributos; omitido, devuelve las de todos los grupos.</param>
    /// <param name="isActive">Filtra por estado activo (<c>true</c>) o inactivo (<c>false</c>); omitido, devuelve todas.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Definiciones de atributo.</response>
    [HttpGet("attribute-definitions")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttributeDefinitionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttributeDefinitions(
        [FromQuery] Guid? groupId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetAttributeDefinitionsQuery(groupId, isActive),
                cancellationToken
            ),
            "OK"
        );

    /// <summary>
    /// Obtiene una definición de atributo por id.
    /// </summary>
    /// <param name="id">Id de la definición de atributo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Definición de atributo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("attribute-definitions/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<AttributeDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttributeDefinitionById(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetAttributeDefinitionByIdQuery(id), cancellationToken)
        );

    /// <summary>
    /// Crea una definición de atributo.
    /// </summary>
    /// <remarks>
    /// Responde 201 con la definición creada.
    /// </remarks>
    /// <param name="command">Datos de la definición, incluido su grupo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Definición creada.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("attribute-definitions")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<AttributeDefinitionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAttributeDefinition(
        [FromBody] CreateAttributeDefinitionCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Actualiza una definición de atributo.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400.
    /// </remarks>
    /// <param name="id">Id de la definición de atributo.</param>
    /// <param name="command">Datos de la definición, con su id.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Definición actualizada.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("attribute-definitions/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<AttributeDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAttributeDefinition(
        Guid id,
        [FromBody] UpdateAttributeDefinitionCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Activa una definición de atributo.
    /// </summary>
    /// <param name="id">Id de la definición de atributo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Definición activada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("attribute-definitions/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableAttributeDefinition(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableAttributeDefinitionCommand(id), cancellationToken)
        );

    /// <summary>
    /// Desactiva una definición de atributo.
    /// </summary>
    /// <remarks>
    /// Baja lógica: la definición no se elimina.
    /// </remarks>
    /// <param name="id">Id de la definición de atributo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Definición desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("attribute-definitions/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAttributeDefinition(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableAttributeDefinitionCommand(id), cancellationToken)
        );
}
