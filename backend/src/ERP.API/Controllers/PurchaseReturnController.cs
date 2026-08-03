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
/// P0-02 Fase 11 — expone los casos de uso ya implementados de <c>PurchaseReturn</c>/
/// <c>SupplierCredit</c> vínculo de NC (Fases 5, 6, 9, 10) — sin permisos nuevos: reutiliza
/// <see cref="PurchasePermissions"/> (mismo criterio ya resuelto en diseño §20.2, idéntico al
/// usado por <see cref="SalesReturnController"/> para <c>SalesReturn</c>). Controller delgado:
/// solo mapea HTTP → MediatR → <c>Result</c>; toda la lógica de negocio (locks, reversas,
/// idempotencia) vive en los handlers ya construidos, reutilizados sin cambios. La aplicación/
/// reembolso de <c>SupplierCredit</c> vive en <see cref="SupplierCreditController"/> (separación
/// de riesgo Compras/Finance, diseño §20.2) — este controller nunca muta crédito financiero.
/// </summary>
[ApiController]
[Route("api/v1/purchases/returns")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseReturnController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseReturnController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // DRAFT
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea un borrador de devolución de compra contra una factura confirmada. La bodega de cada
    /// línea se congela server-side desde la línea original — nunca es un parámetro del comando
    /// (§14.2, decisión de negocio cerrada).
    /// </summary>
    /// <response code="201">Borrador creado.</response>
    /// <response code="404">La factura de compra origen no existe.</response>
    /// <response code="422">Motivo vacío, factura no confirmada, línea inválida o cantidad que excede el remanente.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseReturnDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePurchaseReturnDraftCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Reemplaza por completo motivo y líneas de un borrador. Solo permitido mientras la devolución esté en <c>Draft</c>.</summary>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="404">La devolución o la factura origen no existen.</response>
    /// <response code="422">La devolución ya no está en borrador, línea inválida o cantidad que excede el remanente.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdatePurchaseReturnDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdatePurchaseReturnDraftCommand(id, request.Reason, request.Lines),
                ct
            )
        );

    // ══════════════════════════════════════════════════════════════════════
    // CONSULTAS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Obtiene una devolución de compra por Id, con sus líneas.</summary>
    /// <response code="200">Devolución encontrada.</response>
    /// <response code="404">La devolución no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPurchaseReturnByIdQuery(id), ct));

    /// <summary>Lista devoluciones de compra paginadas, con filtro opcional por estado.</summary>
    /// <response code="200">Listado paginado.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseReturnListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPurchaseReturnListQuery(status, page, pageSize), ct),
            "OK"
        );

    /// <summary>
    /// Líneas de una factura de compra confirmada candidatas a devolución, con el remanente ya
    /// calculado (cantidad original − cantidades ya devueltas en devoluciones <c>Authorized</c>
    /// previas) y la bodega original congelada — nunca seleccionable por el cliente.
    /// </summary>
    /// <response code="200">Líneas devolvibles de la factura.</response>
    /// <response code="404">La factura no existe.</response>
    [HttpGet("/api/v1/purchases/invoices/{invoiceId:guid}/returnable-lines")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<ReturnableLineDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReturnableLines(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetReturnableLinesByPurchaseInvoiceQuery(invoiceId), ct)
        );

    // ══════════════════════════════════════════════════════════════════════
    // AUTORIZACIÓN / CANCELACIÓN / NC
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Autoriza la devolución: congela sus líneas, revierte el inventario correspondiente,
    /// aplica el monto contra la CxP de la factura de origen (creando <c>SupplierCredit</c> por
    /// el excedente si corresponde) y registra el hecho contable, todo en una única transacción.
    /// Idempotente por <c>ClientRequestId</c> (§16.2).
    /// </summary>
    /// <response code="200">Devolución autorizada.</response>
    /// <response code="404">La devolución no existe.</response>
    /// <response code="422">
    /// Sin líneas, retención emitida sobre la factura, stock insuficiente en la bodega original,
    /// o conflicto de idempotencia.
    /// </response>
    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Authorize(
        Guid id,
        [FromBody] AuthorizePurchaseReturnRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new AuthorizePurchaseReturnCommand(id, request.ClientRequestId), ct)
        );

    /// <summary>
    /// Cancela la devolución: sin reversas si está en <c>Draft</c>, o reversa completa todo-o-nada
    /// (inventario + CxP + <c>SupplierCredit</c> si aplica + contabilidad) si está <c>Authorized</c>.
    /// Rechaza si el crédito originado ya tiene aplicaciones o reembolsos activos (<c>PR-011</c>).
    /// Idempotente por <c>ClientRequestId</c> (§16.2).
    /// </summary>
    /// <response code="200">Devolución cancelada.</response>
    /// <response code="404">La devolución no existe.</response>
    /// <response code="422">
    /// Ya cancelada, crédito con aplicaciones/reembolsos activos (PR-011), o conflicto de idempotencia.
    /// </response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelPurchaseReturnRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CancelPurchaseReturnCommand(id, request.Reason, request.ClientRequestId),
                ct
            )
        );

    /// <summary>
    /// Registra (o reutiliza por <c>AccessKey</c>) la Nota de Crédito del proveedor y la vincula
    /// 1:1 a esta devolución autorizada, con la validación cuantitativa obligatoria contra el
    /// monto ya autorizado. Puramente documental — sin efectos de inventario/CxP/crédito/contabilidad.
    /// </summary>
    /// <response code="200">Nota de Crédito vinculada.</response>
    /// <response code="404">La devolución o la factura de origen no existen.</response>
    /// <response code="422">
    /// NC ya vinculada, proveedor/moneda distintos, monto fuera de tolerancia, o conflicto de idempotencia.
    /// </response>
    [HttpPost("{id:guid}/credit-note")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierCreditNoteLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LinkCreditNote(
        Guid id,
        [FromBody] LinkSupplierCreditNoteRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new RegisterAndLinkSupplierCreditNoteCommand(
                    id,
                    request.AccessKey,
                    request.SupplierRuc,
                    request.SupplierName,
                    request.InvoiceNumber,
                    request.IssueDate,
                    request.Subtotal,
                    request.VatAmount,
                    request.TotalAmount,
                    request.CurrencyCode,
                    request.ClientRequestId
                ),
                ct
            )
        );
}

// ── Request DTOs — el Id del agregado va en la ruta, nunca duplicado en el body ─────

/// <summary>Cuerpo de <see cref="PurchaseReturnController.UpdateDraft"/>.</summary>
public sealed record UpdatePurchaseReturnDraftRequest(
    string Reason,
    IReadOnlyList<PurchaseReturnDraftLineInput> Lines
);

/// <summary>Cuerpo de <see cref="PurchaseReturnController.Authorize"/>.</summary>
public sealed record AuthorizePurchaseReturnRequest(Guid ClientRequestId);

/// <summary>Cuerpo de <see cref="PurchaseReturnController.Cancel"/>.</summary>
public sealed record CancelPurchaseReturnRequest(string Reason, Guid ClientRequestId);

/// <summary>Cuerpo de <see cref="PurchaseReturnController.LinkCreditNote"/>.</summary>
public sealed record LinkSupplierCreditNoteRequest(
    string AccessKey,
    string SupplierRuc,
    string SupplierName,
    string InvoiceNumber,
    DateOnly IssueDate,
    decimal Subtotal,
    decimal VatAmount,
    decimal TotalAmount,
    string CurrencyCode,
    Guid ClientRequestId
);
