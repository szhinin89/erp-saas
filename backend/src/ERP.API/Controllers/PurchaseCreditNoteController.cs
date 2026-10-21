using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Entrada única de NC del proveedor: devolución de productos o descuento/bonificación. SSOT: purchases.md.</summary>
[ApiController]
[Route("api/v1/purchases/credit-notes")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseCreditNoteController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseCreditNoteController(IMediator mediator) => _mediator = mediator;

    [HttpGet("historical-returns")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetHistoricalReturns([FromQuery] Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetCreditNoteHistoricalReturnsQuery(invoiceId), ct));

    // ══════════════════════════════════════════════════════════════════════
    // DRAFT
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Crea la NC y su motor interno Return, o vincula una devolución histórica compatible.</summary>
    /// <response code="201">Borrador creado.</response>
    /// <response code="404">La factura afectada, la cuenta por pagar o el documento de recepción no existen.</response>
    /// <response code="422">Datos inválidos, factura sin saldo pendiente, documento de recepción inconsistente, o duplicado.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseCreditNoteDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePurchaseCreditNoteDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateDraftPurchaseCreditNoteCommand(
                    request.ClientRequestId,
                    request.PurchaseInvoiceId,
                    request.ReceptionDocumentId,
                    request.ApplicationType,
                    request.CreditNoteNumber,
                    request.AccessKey,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.IssueDate,
                    request.Reason,
                    request.Lines,
                    request.TaxSummaryLines,
                    request.ReturnLines,
                    request.HistoricalPurchaseReturnId
                ),
                ct
            )
        );

    /// <summary>Reemplaza por completo los datos fiscales/motivo/líneas de un borrador. Solo permitido mientras esté en <c>Draft</c>.</summary>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="404">La nota de crédito no existe.</response>
    /// <response code="422">Ya no está en borrador, datos inválidos o duplicado.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseCreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdatePurchaseCreditNoteDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdatePurchaseCreditNoteDraftCommand(
                    id,
                    request.CreditNoteNumber,
                    request.AccessKey,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.IssueDate,
                    request.Reason,
                    request.Lines,
                    request.TaxSummaryLines
                ),
                ct
            )
        );

    // ══════════════════════════════════════════════════════════════════════
    // CONSULTAS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Obtiene una nota de crédito de compra por Id, con líneas, factura afectada, saldo y datos de recepción vinculados.</summary>
    /// <response code="200">Nota de crédito encontrada.</response>
    /// <response code="404">La nota de crédito no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseCreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPurchaseCreditNoteByIdQuery(id), ct));

    /// <summary>Lista notas de crédito de compra paginadas, con filtros opcionales.</summary>
    /// <response code="200">Listado paginado.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<PurchaseCreditNoteListResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] Guid? invoiceId = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchaseCreditNoteListQuery(
                    status,
                    supplierId,
                    invoiceId,
                    dateFrom,
                    dateTo,
                    page,
                    pageSize
                ),
                ct
            ),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // AUTORIZACIÓN / CANCELACIÓN
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Autoriza la NC según su modalidad; la vinculación histórica no repite efectos.</summary>
    /// <response code="200">Nota de crédito autorizada.</response>
    /// <response code="404">La nota de crédito, la factura o el documento de recepción no existen.</response>
    /// <response code="422">Excede el saldo pendiente, ya autorizada/cancelada, o conflicto de idempotencia.</response>
    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseCreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Authorize(
        Guid id,
        [FromBody] AuthorizePurchaseCreditNoteRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new AuthorizePurchaseCreditNoteCommand(id, request.ClientRequestId),
                ct
            )
        );

    /// <summary>Cancelación interna y reversas canónicas; no anula el comprobante ante el SRI.</summary>
    /// <response code="200">Nota de crédito cancelada.</response>
    /// <response code="404">La nota de crédito no existe.</response>
    /// <response code="422">Ya cancelada o conflicto de idempotencia.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseCreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelPurchaseCreditNoteRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CancelPurchaseCreditNoteCommand(id, request.Reason, request.ClientRequestId),
                ct
            )
        );

}

// ── Request DTOs — el Id del agregado va en la ruta, nunca duplicado en el body ─────

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.CreateDraft"/>.</summary>
public sealed record CreatePurchaseCreditNoteDraftRequest(
    Guid ClientRequestId,
    Guid PurchaseInvoiceId,
    Guid? ReceptionDocumentId,
    ERP.Domain.Modules.Purchases.Enums.PurchaseCreditNoteApplicationType ApplicationType,
    string CreditNoteNumber,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    IReadOnlyList<PurchaseCreditNoteDraftLineInput> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput>? TaxSummaryLines = null,
    IReadOnlyList<PurchaseReturnDraftLineInput>? ReturnLines = null,
    Guid? HistoricalPurchaseReturnId = null
);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.UpdateDraft"/>.</summary>
public sealed record UpdatePurchaseCreditNoteDraftRequest(
    string CreditNoteNumber,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    IReadOnlyList<PurchaseCreditNoteDraftLineInput> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput>? TaxSummaryLines = null
);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.Authorize"/>.</summary>
public sealed record AuthorizePurchaseCreditNoteRequest(Guid ClientRequestId);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.Cancel"/>.</summary>
public sealed record CancelPurchaseCreditNoteRequest(string Reason, Guid ClientRequestId);
