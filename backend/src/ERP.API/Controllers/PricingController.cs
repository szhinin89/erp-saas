using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Pricing.DTOs;
using ERP.Application.Pricing.UseCases.CreatePriceList;
using ERP.Application.Pricing.UseCases.GetPriceLists;
using ERP.Application.Pricing.UseCases.SetItemPrice;
using ERP.Application.Pricing.UseCases.ResolveItemPrice;

namespace ERP.API.Controllers;

[AppFeature("Listas de Precio", "perm:pricing.view", "💰", "/pricing/price-lists", null, 45)]
[ApiController]
[Route("api/pricing")]
[Authorize]
[Produces("application/json")]
public sealed class PricingController : ControllerBase
{
    private readonly IMediator _mediator;
    public PricingController(IMediator mediator) => _mediator = mediator;

    // ── Price Lists ───────────────────────────────────────────────────────

    [HttpGet("price-lists")]
    [Authorize(Policy = "perm:pricing.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceLists(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPriceListsQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost("price-lists")]
    [Authorize(Policy = "perm:pricing.manage")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePriceList(
        [FromBody] CreatePriceListCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Lista de precios creada");
    }

    // ── Entries ───────────────────────────────────────────────────────────

    [HttpPost("price-lists/{priceListId:guid}/entries")]
    [Authorize(Policy = "perm:pricing.manage")]
    [ProducesResponseType(typeof(ApiResponse<PriceListEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetItemPrice(
        Guid priceListId, [FromBody] SetItemPriceRequest request, CancellationToken ct)
    {
        var command = new SetItemPriceCommand(
            priceListId, request.ItemId, request.UomCode, request.Price,
            request.MinQty, request.VariantId);
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Precio actualizado");
    }

    // ── Resolve ───────────────────────────────────────────────────────────

    [HttpGet("resolve")]
    [Authorize(Policy = "perm:pricing.view")]
    [ProducesResponseType(typeof(ApiResponse<ResolvedPriceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolvePrice(
        [FromQuery] Guid     itemId,
        [FromQuery] string   uomCode,
        [FromQuery] decimal  qty       = 1,
        [FromQuery] Guid?    variantId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ResolveItemPriceQuery(itemId, uomCode, qty, variantId), ct);
        return this.ToOkOrNotFound(result);
    }
}

public record SetItemPriceRequest(
    Guid    ItemId,
    string  UomCode,
    decimal Price,
    decimal MinQty    = 1,
    Guid?   VariantId = null
);
