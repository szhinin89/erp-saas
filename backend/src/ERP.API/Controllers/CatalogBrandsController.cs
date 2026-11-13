using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.Brands;
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
public sealed class CatalogBrandsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogBrandsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // BRANDS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las marcas de ítems.
    /// </summary>
    /// <remarks>
    /// Las marcas son un catálogo propio de cada tenant. Se puede filtrar por estado.
    /// </remarks>
    /// <param name="isActive">Filtra por estado activo (<c>true</c>) o inactivo (<c>false</c>); omitido, devuelve todas.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Marcas.</response>
    [HttpGet("brands")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BrandDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBrands(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetBrandsQuery(isActive), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Obtiene una marca por id.
    /// </summary>
    /// <param name="id">Id de la marca.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Marca.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("brands/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBrandById(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetBrandByIdQuery(id), cancellationToken));

    /// <summary>
    /// Crea una marca.
    /// </summary>
    /// <remarks>
    /// Responde 201 con la marca creada. Un ítem nuevo solo puede usar una marca activa.
    /// </remarks>
    /// <param name="command">Datos de la marca.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Marca creada.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("brands")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBrand(
        [FromBody] CreateBrandCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Actualiza una marca.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400.
    /// </remarks>
    /// <param name="id">Id de la marca.</param>
    /// <param name="command">Datos de la marca, con su id.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Marca actualizada.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPut("brands/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBrand(
        Guid id,
        [FromBody] UpdateBrandCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Activa una marca.
    /// </summary>
    /// <param name="id">Id de la marca.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Marca activada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("brands/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableBrand(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableBrandCommand(id), cancellationToken));

    /// <summary>
    /// Desactiva una marca.
    /// </summary>
    /// <remarks>
    /// Baja lógica: la marca no se elimina ni deja de estar asociada a los ítems que ya la usan.
    /// </remarks>
    /// <param name="id">Id de la marca.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Marca desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("brands/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableBrand(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableBrandCommand(id), cancellationToken));
}
