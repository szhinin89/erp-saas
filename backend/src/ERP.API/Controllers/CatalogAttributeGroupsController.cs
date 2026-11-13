using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.AttributeGroups;
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
public sealed class CatalogAttributeGroupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogAttributeGroupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE GROUPS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista los grupos de atributos.
    /// </summary>
    /// <remarks>
    /// Los grupos agrupan las definiciones de atributo que describen las variantes de un ítem. Catálogo propio de
    /// cada tenant.
    /// </remarks>
    /// <param name="isActive">Filtra por estado activo (<c>true</c>) o inactivo (<c>false</c>); omitido, devuelve todos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Grupos de atributos.</response>
    [HttpGet("attribute-groups")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttributeGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttributeGroups(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetAttributeGroupsQuery(isActive), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Obtiene un grupo de atributos por id.
    /// </summary>
    /// <param name="id">Id de el grupo de atributos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Grupo de atributos.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("attribute-groups/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<AttributeGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttributeGroupById(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetAttributeGroupByIdQuery(id), cancellationToken)
        );

    /// <summary>
    /// Crea un grupo de atributos.
    /// </summary>
    /// <remarks>
    /// Responde 201 con el grupo creado.
    /// </remarks>
    /// <param name="command">Datos del grupo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Grupo creado.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("attribute-groups")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<AttributeGroupDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAttributeGroup(
        [FromBody] CreateAttributeGroupCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Actualiza un grupo de atributos.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400.
    /// </remarks>
    /// <param name="id">Id de el grupo de atributos.</param>
    /// <param name="command">Datos del grupo, con su id.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Grupo actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("attribute-groups/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<AttributeGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAttributeGroup(
        Guid id,
        [FromBody] UpdateAttributeGroupCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Activa un grupo de atributos.
    /// </summary>
    /// <param name="id">Id de el grupo de atributos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Grupo activado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("attribute-groups/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableAttributeGroup(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableAttributeGroupCommand(id), cancellationToken)
        );

    /// <summary>
    /// Desactiva un grupo de atributos.
    /// </summary>
    /// <remarks>
    /// Baja lógica: el grupo no se elimina.
    /// </remarks>
    /// <param name="id">Id de el grupo de atributos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Grupo desactivado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("attribute-groups/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAttributeGroup(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableAttributeGroupCommand(id), cancellationToken)
        );
}
