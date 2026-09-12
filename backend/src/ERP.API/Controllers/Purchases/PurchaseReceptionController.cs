using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.FindItemMatches;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.GetLineMatch;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.UnmatchItem;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreateExpenseDraftFromReception;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Purchases;

[AppFeature(
    "Recepción electrónica",
    $"perm:{PurchasePermissions.View}",
    "move_to_inbox",
    "/purchases/reception",
    $"perm:{PurchasePermissions.View}",
    61
)]
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
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionImportResultDto>),
        StatusCodes.Status200OK
    )]
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
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<DownloadPurchaseReceptionXmlResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> DownloadXml(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DownloadPurchaseReceptionXmlCommand(id), ct)
        );

    /// <summary>
    /// PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 — descarga en lote el XML autorizado en el SRI
    /// solo para documentos que aún no lo tienen. Reutiliza <see cref="DownloadXml"/> por
    /// documento (mismo caso de uso, sin lógica duplicada); no crea compras, gastos ni notas de
    /// crédito.
    /// </summary>
    [HttpPost("documents/download-xml-pending")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<BatchDownloadPurchaseReceptionXmlResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> DownloadXmlPending(
        [FromBody] BatchDownloadPurchaseReceptionXmlRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new BatchDownloadPurchaseReceptionXmlCommand(body.DocumentIds, body.OnlyMissingXml),
                ct
            )
        );

    [HttpPost("{id:guid}/create-draft")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<PurchaseDraftDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateDraft(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CreatePurchaseReceptionDraftCommand(id), ct)
        );

    /// <summary>
    /// EXPENSES-FROM-RECEPTION-01 — cabecera de solo lectura para precargar el formulario de
    /// Nuevo Gasto. Solo facturas (nunca notas de crédito); nunca crea ni persiste el
    /// <c>ExpenseDocument</c> — eso ocurre al guardar desde el formulario de Gastos.
    /// </summary>
    [HttpPost("{id:guid}/create-expense-draft")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ExpenseReceptionDraftDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> CreateExpenseDraft(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CreateExpenseDraftFromReceptionQuery(id), ct)
        );

    /// <summary>
    /// Vista de solo lectura del XML ya guardado en recepción electrónica (FLOW-READY-02E.1) —
    /// nunca descarga, reprocesa ni cambia el estado del documento.
    /// </summary>
    [HttpGet("documents/{id:guid}/xml-view")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionXmlViewDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetXmlView(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPurchaseReceptionXmlViewQuery(id), ct)
        );

    // ── Item Matching ────────────────────────────────────────────────────

    [HttpGet("{id:guid}/lines")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<PurchaseReceptionLineMatchDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetLines(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new FindItemMatchesQuery(id), ct));

    [HttpGet("lines/{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetLineMatch(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPurchaseReceptionLineMatchQuery(id), ct));

    [HttpPost("lines/{id:guid}/match-item")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> MatchItem(
        Guid id,
        [FromBody] MatchItemRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new MatchItemCommand(id, body.ItemId, body.PackagingLevelId), ct)
        );

    [HttpPost("lines/{id:guid}/unmatch-item")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> UnmatchItem(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new UnmatchPurchaseReceptionItemCommand(id), ct)
        );

    [HttpPost("matching/bulk")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<BulkMatchItemsResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> BulkMatch(
        [FromBody] IReadOnlyList<BulkMatchItemEntry> matches,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(new BulkMatchItemsCommand(matches), ct));
}

public sealed record MatchItemRequest(Guid ItemId, Guid? PackagingLevelId = null);

public sealed record BatchDownloadPurchaseReceptionXmlRequest(
    IReadOnlyList<Guid> DocumentIds,
    bool OnlyMissingXml = true
);
