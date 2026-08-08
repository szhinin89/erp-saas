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

    [HttpPut("{id:guid}/images")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
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

    [HttpPatch("{id:guid}/images/{imageId:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
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
