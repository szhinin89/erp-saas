using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.FindItemMatches;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using ERP.Application.Modules.Purchases.UseCases.PurchaseReception.CreateItemFromReceptionLine;
using ERP.Domain.Kernel.Permissions;

namespace ERP.API.Controllers.Purchases;

[AppFeature("Recepción electrónica", $"perm:{PurchasePermissions.View}", "move_to_inbox", "/purchases/reception", $"perm:{PurchasePermissions.View}", 61)]
[ApiController]
[Route("api/v1/purchases/reception")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseReceptionController : ControllerBase
{
    private readonly IMediator _mediator;
    public PurchaseReceptionController(IMediator mediator) => _mediator = mediator;

    [HttpPost("import")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<PurchaseReceptionImportResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        var content = new MediaUploadContent(stream, file.FileName, file.ContentType, file.Length);
        var result = await _mediator.Send(new ImportPurchaseReceptionCommand(content), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("{id:guid}/download-xml")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<DownloadPurchaseReceptionXmlResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadXml(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DownloadPurchaseReceptionXmlCommand(id), ct));

    [HttpPost("{id:guid}/create-draft")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<PurchaseDraftDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateDraft(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new CreatePurchaseReceptionDraftCommand(id), ct));

    // ── Item Matching ────────────────────────────────────────────────────

    [HttpGet("{id:guid}/lines")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<IReadOnlyList<PurchaseReceptionLineMatchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLines(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new FindItemMatchesQuery(id), ct));

    [HttpPost("lines/{id:guid}/match-item")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MatchItem(Guid id, [FromBody] MatchItemRequest body, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new MatchItemCommand(id, body.ItemId), ct));

    [HttpPost("matching/bulk")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<BulkMatchItemsResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkMatch([FromBody] IReadOnlyList<BulkMatchItemEntry> matches, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new BulkMatchItemsCommand(matches), ct));

    [HttpPost("lines/{id:guid}/create-item")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<CreateItemFromReceptionLineResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateItemFromLine(Guid id, [FromBody] CreateItemFromReceptionLineRequest body, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new CreateItemFromReceptionLineCommand(
            id, body.Sku, body.ShortName, body.Description, body.ItemTypeId,
            body.CategoryNodeId, body.BrandId, body.DefaultUomCode, body.BarcodeType), ct));
}

public sealed record MatchItemRequest(Guid ItemId);

public sealed record CreateItemFromReceptionLineRequest(
    string Sku, string ShortName, string Description, Guid ItemTypeId,
    Guid CategoryNodeId, Guid BrandId, string DefaultUomCode, string BarcodeType);
