using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.ItemImages;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// Split out of ItemsController (B-controller max-lines) — mismo Route base, mismos
// endpoints, mismos permisos. Ver ItemsController.cs para el resto del recurso Items.
[ApiController]
[Route("api/v1/items")]
[Authorize]
[Produces("application/json")]
public sealed class ItemImagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemImagesController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // IMAGES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reemplaza el conjunto de imágenes de un ítem.
    /// </summary>
    /// <remarks>
    /// El conjunto enviado sustituye al actual: las imágenes anteriores se eliminan y se crean las indicadas. Cada
    /// imagen referencia un objeto ya almacenado (<c>storageObjectId</c>), con texto alternativo, indicadores de
    /// principal y de comercio electrónico, orden y variante opcional. La operación no sube archivos. Se ejecuta en
    /// una transacción con bloqueo del ítem.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Lista de imágenes que quedarán asociadas al ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Imágenes del ítem actualizadas.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/images")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceImages(
        Guid id,
        [FromBody] ReplaceImagesRequest request,
        CancellationToken cancellationToken
    )
    {
        var cmd = new ReplaceItemImagesCommand(
            id,
            request
                .Images.Select(i => new ImageInput(
                    i.StorageObjectId,
                    i.AltText,
                    i.IsMain,
                    i.IsEcommerce,
                    i.SortOrder,
                    i.VariantId
                ))
                .ToList()
        );
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, cancellationToken));
    }

    /// <summary>
    /// Reactiva una imagen inactiva de un ítem.
    /// </summary>
    /// <remarks>
    /// Conserva el id, el objeto almacenado, la variante, el texto alternativo y el orden de la imagen; la deja
    /// activa y no principal. No modifica la imagen principal vigente. Si la imagen ya estaba activa responde con
    /// su estado actual sin guardar cambios. No verifica la existencia física del objeto almacenado.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="imageId">Id de la imagen del ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Imagen reactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/images/{imageId:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemImageDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableImage(Guid id, Guid imageId, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableItemImageCommand(id, imageId), cancellationToken));

    /// <summary>
    /// Desactiva una imagen de un ítem.
    /// </summary>
    /// <remarks>
    /// Baja lógica: la imagen no se elimina.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="imageId">Id de la imagen del ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Imagen desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/images/{imageId:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableItemImageCommand(id, imageId), cancellationToken)
        );
}

// ── Request body types ─────────────────────────────────────────────────────

public record ReplaceImagesRequest(IReadOnlyList<ImageApiInput> Images);

public record ImageApiInput(
    Guid StorageObjectId,
    string? AltText = null,
    bool IsMain = false,
    bool IsEcommerce = false,
    int SortOrder = 0,
    Guid? VariantId = null
);
