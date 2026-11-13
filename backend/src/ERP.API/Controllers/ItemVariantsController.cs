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

    /// <summary>
    /// Agrega una variante a un ítem.
    /// </summary>
    /// <remarks>
    /// Responde 201 con la variante creada. El SKU de la variante se deriva de los atributos de eje o se fija con
    /// <c>skuOverride</c>; es único por empresa. La combinación de atributos no puede repetirse entre variantes
    /// activas.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Atributos de la variante, SKU opcional y orden.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Variante creada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("{id:guid}/variants")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemVariantDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
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

    /// <summary>
    /// Actualiza una variante de un ítem.
    /// </summary>
    /// <remarks>
    /// Permite cambiar el orden y marcar la variante como predeterminada.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="variantId">Id de la variante del ítem.</param>
    /// <param name="request">Orden e indicador de variante predeterminada.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Variante actualizada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Desactiva una variante de un ítem.
    /// </summary>
    /// <remarks>
    /// Baja lógica: la variante no se elimina.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="variantId">Id de la variante del ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Variante desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/variants/{variantId:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableVariant(
        Guid id,
        Guid variantId,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisableItemVariantCommand(id, variantId), cancellationToken)
        );

    /// <summary>
    /// Activa una variante de un ítem.
    /// </summary>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="variantId">Id de la variante del ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Variante activada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/variants/{variantId:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Agrega un código de barras a una variante.
    /// </summary>
    /// <remarks>
    /// Responde 201 con el código creado. El código es único por empresa entre variantes y presentaciones.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="variantId">Id de la variante del ítem.</param>
    /// <param name="request">Código y tipo de código de barras.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Código de barras creado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("{id:guid}/variants/{variantId:guid}/barcodes")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<VariantBarcodeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
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

    /// <summary>
    /// Desactiva un código de barras de una variante.
    /// </summary>
    /// <remarks>
    /// Baja lógica: el código no se elimina.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="variantId">Id de la variante del ítem.</param>
    /// <param name="barcodeId">Id del código de barras.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Código de barras desactivado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/variants/{variantId:guid}/barcodes/{barcodeId:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
