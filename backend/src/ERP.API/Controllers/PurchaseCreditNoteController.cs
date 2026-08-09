using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// FLOW-READY-02C.2 — expone los casos de uso de <c>PurchaseCreditNote</c> (nota de crédito de
/// compra por descuento/promoción, v1). Sin permisos nuevos: reutiliza <see cref="PurchasePermissions"/>,
/// mismo criterio que <see cref="PurchaseReturnController"/>. Controller delgado: solo mapea
/// HTTP → MediatR → <c>Result</c>. La devolución física de productos sigue siendo responsabilidad
/// exclusiva de <c>/api/v1/purchases/returns</c> (<see cref="PurchaseReturnController"/>) — este
/// controller nunca la reimplementa (decisión de arquitectura, diseño FLOW-READY-02C §0.1).
/// </summary>
[ApiController]
[Route("api/v1/purchases/credit-notes")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseCreditNoteController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseCreditNoteController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // DRAFT
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea un borrador de nota de crédito de compra (descuento/promoción) contra una factura
    /// confirmada con saldo pendiente. Acepta un <c>ReceptionDocumentId</c> opcional para vincular
    /// una Nota de Crédito recibida del proveedor (TXT/XML) — validado como referencia, nunca como
    /// fuente de inventario.
    /// </summary>
    /// <response code="201">Borrador creado.</response>
    /// <response code="404">La factura afectada, la cuenta por pagar o el documento de recepción no existen.</response>
    /// <response code="422">Datos inválidos, factura sin saldo pendiente, documento de recepción inconsistente, o duplicado.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(
        typeof(ApiResponse<PurchaseCreditNoteDto>),
        StatusCodes.Status201Created
    )]
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
                    request.TaxSummaryLines
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

    /// <summary>
    /// Autoriza la nota de crédito: bloquea si el total excede el saldo pendiente de la factura
    /// (nunca trunca ni genera <c>SupplierCredit</c>), aplica el monto contra la CxP, congela el
    /// snapshot financiero y marca procesado el documento de recepción vinculado, si existe. Nunca
    /// mueve inventario ni genera asientos contables. Idempotente por <c>ClientRequestId</c>.
    /// </summary>
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

    /// <summary>
    /// Cancela la nota de crédito: sin reversas si está en <c>Draft</c>, o reversa de
    /// <c>PurchasePayable.CreditNoteAppliedAmount</c> si está <c>Authorized</c>. Nunca mueve
    /// inventario ni toca contabilidad. Idempotente por <c>ClientRequestId</c>.
    /// </summary>
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

    // ══════════════════════════════════════════════════════════════════════
    // VÍNCULO CON DEVOLUCIÓN (FLOW-READY-02C-R1.1)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vincula una nota de crédito tipo Devolución al <c>PurchaseReturn</c> que realmente la aplica.
    /// Puramente documental — nunca mueve inventario, CxP ni contabilidad (eso es responsabilidad
    /// exclusiva de <c>/api/v1/purchases/returns/{id}/authorize</c>). Set-once. Idempotente por
    /// <c>ClientRequestId</c>.
    /// </summary>
    /// <response code="200">Nota de crédito vinculada.</response>
    /// <response code="404">La nota de crédito o la devolución de compra no existen.</response>
    /// <response code="422">Tipo de aplicación distinto de Devolución, ya vinculada, ya no está en borrador, o datos inconsistentes (factura/proveedor/empresa/sucursal).</response>
    [HttpPost("{id:guid}/link-return")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseCreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LinkReturn(
        Guid id,
        [FromBody] LinkPurchaseCreditNoteToReturnRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new LinkPurchaseCreditNoteToReturnCommand(
                    id,
                    request.PurchaseReturnId,
                    request.ClientRequestId
                ),
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
    DateOnly? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    IReadOnlyList<PurchaseCreditNoteDraftLineInput> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput>? TaxSummaryLines = null
);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.UpdateDraft"/>.</summary>
public sealed record UpdatePurchaseCreditNoteDraftRequest(
    string CreditNoteNumber,
    string? AccessKey,
    string? AuthorizationNumber,
    DateOnly? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    IReadOnlyList<PurchaseCreditNoteDraftLineInput> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryLineInput>? TaxSummaryLines = null
);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.Authorize"/>.</summary>
public sealed record AuthorizePurchaseCreditNoteRequest(Guid ClientRequestId);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.Cancel"/>.</summary>
public sealed record CancelPurchaseCreditNoteRequest(string Reason, Guid ClientRequestId);

/// <summary>Cuerpo de <see cref="PurchaseCreditNoteController.LinkReturn"/>.</summary>
public sealed record LinkPurchaseCreditNoteToReturnRequest(Guid PurchaseReturnId, Guid ClientRequestId);
