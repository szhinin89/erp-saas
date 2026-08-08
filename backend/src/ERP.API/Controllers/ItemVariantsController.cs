using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.AddItemVariant;
using ERP.Application.Items.UseCases.Barcodes;
using ERP.Application.Items.UseCases.DisableItemVariant;
using ERP.Application.Items.UseCases.EnableItemVariant;
using ERP.Application.Items.UseCases.UpdateItemVariant;
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
public sealed class ItemVariantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemVariantsController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // VARIANTS
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost("{id:guid}/variants")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemVariantDto>), 201)]
    public async Task<IActionResult> AddVariant(
        Guid id,
        [FromBody] AddVariantRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new AddItemVariantCommand(
            id,
            request
                .Attributes.Select(a => new VariantAttributeInput(a.AttributeDefinitionId, a.Value))
                .ToList(),
            request.SkuOverride,
            request.SortOrder
        );
        return this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> UpdateVariant(
        Guid id,
        Guid variantId,
        [FromBody] UpdateVariantRequest request,
        CancellationToken cancellationToken
    )
    {
        var cmd = new UpdateItemVariantCommand(id, variantId, request.SortOrder, request.IsDefault);
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, cancellationToken));
    }

    [HttpPatch("{id:guid}/variants/{variantId:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> DisableVariant(
        Guid id,
        Guid variantId,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableItemVariantCommand(id, variantId), cancellationToken)
        );

    [HttpPatch("{id:guid}/variants/{variantId:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> EnableVariant(
        Guid id,
        Guid variantId,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnableItemVariantCommand(id, variantId), cancellationToken)
        );

    // ══════════════════════════════════════════════════════════════════════
    // BARCODES
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost("{id:guid}/variants/{variantId:guid}/barcodes")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> AddBarcode(
        Guid id,
        Guid variantId,
        [FromBody] AddBarcodeRequest request,
        CancellationToken cancellationToken
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new AddBarcodeCommand(id, variantId, request.Code, request.BarcodeType),
                cancellationToken
            )
        );

    [HttpPatch("{id:guid}/variants/{variantId:guid}/barcodes/{barcodeId:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> DisableBarcode(
        Guid id,
        Guid variantId,
        Guid barcodeId,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new DisableBarcodeCommand(id, variantId, barcodeId),
                cancellationToken
            )
        );
}

// ── Request body types ─────────────────────────────────────────────────────

public record AddVariantRequest(
    IReadOnlyList<VariantAttrInput> Attributes,
    string? SkuOverride = null,
    int SortOrder = 0
);

public record VariantAttrInput(Guid AttributeDefinitionId, string Value);

public record UpdateVariantRequest(int SortOrder, bool IsDefault = false);

public record AddBarcodeRequest(string Code, string BarcodeType);
