using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.DisableItem;
using ERP.Application.Items.UseCases.EnableItem;
using ERP.Application.Items.UseCases.GetItemById;
using ERP.Application.Items.UseCases.GetItemReport;
using ERP.Application.Items.UseCases.GetItems;
using ERP.Application.Items.UseCases.ResolveItem;
using ERP.Application.Items.UseCases.UpdateItem;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Ítems", $"perm:{InventoryPermissions.ItemsView}", "📦", "/inventory/items", null, 35)]
[ApiController]
[Route("api/v1/items")]
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
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<GetItemsResponse>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? sku = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isForSale = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] bool? isEcommerce = null,
        [FromQuery] Guid? itemTypeId = null,
        [FromQuery] Guid? categoryNodeId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] string? barcode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetItemsQuery(
                search,
                sku,
                isActive,
                isForSale,
                isFavorite,
                isEcommerce,
                itemTypeId,
                categoryNodeId,
                brandId,
                barcode,
                pageNumber,
                pageSize
            ),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("resolve/{code}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    public async Task<IActionResult> ResolveItem(
        string code,
        CancellationToken cancellationToken
    ) => this.ToOkOrNotFound(await _mediator.Send(new ResolveItemQuery(code), cancellationToken));

    [HttpGet("report")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<GetItemsResponse>), 200)]
    public async Task<IActionResult> GetReport(
        [FromQuery] string? search = null,
        [FromQuery] string? sku = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isForSale = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] bool? isEcommerce = null,
        [FromQuery] Guid? itemTypeId = null,
        [FromQuery] Guid? categoryNodeId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] string? barcode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetItemReportQuery(
                search,
                sku,
                isActive,
                isForSale,
                isFavorite,
                isEcommerce,
                itemTypeId,
                categoryNodeId,
                brandId,
                barcode,
                pageNumber,
                pageSize
            ),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ══════════════════════════════════════════════════════════════════════
    // DETAIL
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetItemByIdQuery(id), cancellationToken));

    [HttpGet("{id:guid}/full-report")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemFullReportDto>), 200)]
    public async Task<IActionResult> GetFullReport(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetItemFullReportQuery(id), cancellationToken)
        );

    // ══════════════════════════════════════════════════════════════════════
    // CREATE / UPDATE
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDto>), 201)]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateItemCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableItemCommand(id), cancellationToken));

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableItemCommand(id), cancellationToken));
}
