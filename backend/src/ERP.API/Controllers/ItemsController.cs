using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.UpdateItem;
using ERP.Application.Items.UseCases.EnableItem;
using ERP.Application.Items.UseCases.DisableItem;
using ERP.Application.Items.UseCases.GetItemById;
using ERP.Application.Items.UseCases.GetItems;
using ERP.Application.Items.UseCases.GetItemReport;
using ERP.Application.Items.UseCases.AddItemVariant;
using ERP.Application.Items.UseCases.UpdateItemVariant;
using ERP.Application.Items.UseCases.DisableItemVariant;
using ERP.Application.Items.UseCases.EnableItemVariant;
using ERP.Application.Items.UseCases.ItemImages;
using ERP.Application.Items.UseCases.ItemUnitConversions;
using ERP.Application.Items.UseCases.ItemSubstitutes;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
using ERP.Application.Items.UseCases.GetStockByItem;
using ERP.Application.Items.UseCases.GetStockKardex;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.API.Controllers;

[AppFeature("Ítems", "perm:items.view", "📦", "/items", null, 35)]
[ApiController]
[Route("api/items")]
[Authorize]
[Produces("application/json")]
public sealed class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ItemsController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // LIST + REPORTS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet]
    [Authorize(Policy = "perm:items.view")]
    [ProducesResponseType(typeof(ApiResponse<GetItemsResponse>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null, [FromQuery] string? sku = null,
        [FromQuery] bool? isActive = null, [FromQuery] bool? isForSale = null,
        [FromQuery] bool? isFavorite = null, [FromQuery] bool? isEcommerce = null,
        [FromQuery] ItemType? itemType = null, [FromQuery] Guid? categoryNodeId = null,
        [FromQuery] Guid? brandId = null, [FromQuery] string? barcode = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetItemsQuery(
            search, sku, isActive, isForSale, isFavorite, isEcommerce,
            itemType, categoryNodeId, brandId, barcode, pageNumber, pageSize), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("report")]
    [Authorize(Policy = "perm:items.view")]
    [ProducesResponseType(typeof(ApiResponse<GetItemsResponse>), 200)]
    public async Task<IActionResult> GetReport(
        [FromQuery] string? search = null, [FromQuery] string? sku = null,
        [FromQuery] bool? isActive = null, [FromQuery] bool? isForSale = null,
        [FromQuery] bool? isFavorite = null, [FromQuery] bool? isEcommerce = null,
        [FromQuery] ItemType? itemType = null, [FromQuery] Guid? categoryNodeId = null,
        [FromQuery] Guid? brandId = null, [FromQuery] string? barcode = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetItemReportQuery(
            search, sku, isActive, isForSale, isFavorite, isEcommerce,
            itemType, categoryNodeId, brandId, barcode, pageNumber, pageSize), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ══════════════════════════════════════════════════════════════════════
    // DETAIL
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:items.view")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetItemByIdQuery(id), ct));

    [HttpGet("{id:guid}/full-report")]
    [Authorize(Policy = "perm:items.view")]
    [ProducesResponseType(typeof(ApiResponse<ItemFullReportDto>), 200)]
    public async Task<IActionResult> GetFullReport(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetItemFullReportQuery(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // CREATE / UPDATE
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [Authorize(Policy = "perm:items.create")]
    [ProducesResponseType(typeof(ApiResponse<ItemDto>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateItemCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Ítem creado");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateItemCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct), "Actualizado");
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableItemCommand(id), ct), "Deshabilitado");

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableItemCommand(id), ct), "Habilitado");

    // ══════════════════════════════════════════════════════════════════════
    // VARIANTS
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost("{id:guid}/variants")]
    [Authorize(Policy = "perm:items.edit")]
    [ProducesResponseType(typeof(ApiResponse<ItemVariantDto>), 201)]
    public async Task<IActionResult> AddVariant(
        Guid id, [FromBody] AddVariantRequest request, CancellationToken ct)
    {
        var command = new AddItemVariantCommand(
            id,
            request.Attributes.Select(a => new VariantAttributeInput(a.AttributeDefinitionId, a.Value)).ToList(),
            request.SkuOverride, request.SortOrder);
        return this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Variante creada");
    }

    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> UpdateVariant(
        Guid id, Guid variantId, [FromBody] UpdateVariantRequest request, CancellationToken ct)
    {
        var cmd = new UpdateItemVariantCommand(id, variantId, request.SortOrder, request.IsDefault);
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Variante actualizada");
    }

    [HttpPatch("{id:guid}/variants/{variantId:guid}/disable")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> DisableVariant(Guid id, Guid variantId, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableItemVariantCommand(id, variantId), ct), "Deshabilitada");

    [HttpPatch("{id:guid}/variants/{variantId:guid}/enable")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> EnableVariant(Guid id, Guid variantId, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableItemVariantCommand(id, variantId), ct), "Habilitada");

    // ══════════════════════════════════════════════════════════════════════
    // IMAGES
    // ══════════════════════════════════════════════════════════════════════

    [HttpPut("{id:guid}/images")]
    [Authorize(Policy = "perm:items.edit")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> ReplaceImages(
        Guid id, [FromBody] ReplaceImagesRequest request, CancellationToken ct)
    {
        var cmd = new ReplaceItemImagesCommand(id,
            request.Images.Select(i => new ImageInput(
                i.StorageObjectId, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder, i.VariantId)).ToList());
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Imágenes actualizadas");
    }

    [HttpPatch("{id:guid}/images/{imageId:guid}/disable")]
    [Authorize(Policy = "perm:items.edit")]
    public async Task<IActionResult> DisableImage(Guid id, Guid imageId, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableItemImageCommand(id, imageId), ct), "Imagen deshabilitada");

    // ══════════════════════════════════════════════════════════════════════
    // UNIT CONVERSIONS
    // ══════════════════════════════════════════════════════════════════════

    [HttpPut("{id:guid}/unit-conversions")]
    [Authorize(Policy = "perm:items.edit")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> ReplaceUnitConversions(
        Guid id, [FromBody] ReplaceConversionsRequest request, CancellationToken ct)
    {
        var cmd = new ReplaceItemUnitConversionsCommand(id,
            request.Conversions.Select(c => new UnitConversionInput(c.FromUomCode, c.ToUomCode, c.Factor)).ToList());
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Conversiones actualizadas");
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUBSTITUTES
    // ══════════════════════════════════════════════════════════════════════

    [HttpPut("{id:guid}/substitutes")]
    [Authorize(Policy = "perm:items.edit")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> ReplaceSubstitutes(
        Guid id, [FromBody] ReplaceSubstitutesRequest request, CancellationToken ct)
    {
        var cmd = new ReplaceItemSubstitutesCommand(id,
            request.Substitutes.Select(s => new SubstituteInput(s.SubstituteItemId, s.Priority, s.Note)).ToList());
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Sustitutos actualizados");
    }

    // ══════════════════════════════════════════════════════════════════════
    // PACKAGING LEVELS
    // ══════════════════════════════════════════════════════════════════════

    [HttpPut("{id:guid}/packaging-levels")]
    [Authorize(Policy = "perm:items.edit")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> ReplacePackagingLevels(
        Guid id, [FromBody] ReplacePackagingRequest request, CancellationToken ct)
    {
        var cmd = new ReplaceItemPackagingLevelsCommand(id,
            request.Levels.Select(l => new PackagingLevelInput(
                l.Name, l.Level, l.BaseQuantity, l.UomCode,
                l.Barcode, l.Weight, l.IsBaseUnit, l.IsPurchaseDefault, l.IsSaleDefault)).ToList());
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Empaques actualizados");
    }

    // ══════════════════════════════════════════════════════════════════════
    // STOCK + KARDEX
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("{id:guid}/stock")]
    [Authorize(Policy = "perm:items.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockByItemDto>>), 200)]
    public async Task<IActionResult> GetStock(
        Guid id, [FromQuery] Guid? warehouseId = null, CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetStockByItemQuery(id, warehouseId), ct), "OK");

    [HttpGet("{id:guid}/kardex")]
    [Authorize(Policy = "perm:items.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), 200)]
    public async Task<IActionResult> GetKardex(
        Guid id,
        [FromQuery] Guid? variantId   = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] DateTime? from    = null,
        [FromQuery] DateTime? to      = null,
        [FromQuery] int pageNumber    = 1,
        [FromQuery] int pageSize      = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStockKardexQuery(
            id, variantId, warehouseId, from, to, pageNumber, pageSize), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }
}

// ── Request body types ─────────────────────────────────────────────────────

public record AddVariantRequest(
    IReadOnlyList<VariantAttrInput> Attributes,
    string? SkuOverride = null,
    int SortOrder = 0
);

public record VariantAttrInput(Guid AttributeDefinitionId, string Value);

public record UpdateVariantRequest(int SortOrder, bool IsDefault = false);

public record ReplaceImagesRequest(IReadOnlyList<ImageApiInput> Images);
public record ImageApiInput(
    Guid StorageObjectId, string? AltText = null,
    bool IsMain = false, bool IsEcommerce = false, int SortOrder = 0, Guid? VariantId = null);

public record ReplaceConversionsRequest(IReadOnlyList<ConversionApiInput> Conversions);
public record ConversionApiInput(string FromUomCode, string ToUomCode, decimal Factor);

public record ReplaceSubstitutesRequest(IReadOnlyList<SubstituteApiInput> Substitutes);
public record SubstituteApiInput(Guid SubstituteItemId, int Priority = 1, string? Note = null);

public record ReplacePackagingRequest(IReadOnlyList<PackagingApiInput> Levels);
public record PackagingApiInput(
    string Name, int Level, decimal BaseQuantity, string UomCode,
    string? Barcode = null, decimal? Weight = null,
    bool IsBaseUnit = false, bool IsPurchaseDefault = false, bool IsSaleDefault = false);
