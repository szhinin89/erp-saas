using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Purchases.UseCases.GetPurchaseItemContext;
using ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Compras", $"perm:{PurchasePermissions.View}", "🛒", "/purchases", null, 60)]
[ApiController]
[Route("api/v1/purchases")]
[Authorize]
[Produces("application/json")]
public sealed class PurchasesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchasesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePurchaseDraftCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdatePurchaseDraftCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPurchaseByIdQuery(id), ct));

    [HttpGet("by-access-key/{accessKey}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetByAccessKey(string accessKey, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPurchaseByAccessKeyQuery(accessKey), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? supplierId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchaseListQuery(search, status, pageNumber, pageSize, supplierId),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Reporte básico de compras por proveedor (piloto Sumak). Sin fechas, usa el día actual
    /// (UTC). Company-scoped — ver GetPurchasesBySupplierReportQuery.
    /// </summary>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetSupplierReport(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] Guid? supplierId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchasesBySupplierReportQuery(dateFrom, dateTo, supplierId),
                ct
            ),
            "OK"
        );

    [HttpPost("{id:guid}/apply-discount")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> ApplyGlobalDiscount(
        Guid id,
        [FromBody] ApplyDiscountRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ApplyGlobalDiscountCommand(id, request.DiscountPct), ct)
        );

    [HttpPost("{id:guid}/allocate-freight")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> AllocateFreight(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new AllocateFreightCommand(id), ct));

    [HttpPost("{id:guid}/recalculate")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> Recalculate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new RecalculatePurchaseCommand(id), ct));

    /// <summary>
    /// PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — aplica el prorrateo revisado en el modal
    /// "Distribuir flete/gasto": suma <c>Amount</c> a las líneas de <c>IncludedLineIds</c>,
    /// proporcional a su base imponible. No toca líneas fuera de la selección.
    /// </summary>
    [HttpPost("{id:guid}/distribute-cost")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> DistributeCost(
        Guid id,
        [FromBody] DistributeCostRequest request,
        CancellationToken ct
    )
    {
        // PURCHASE-COSTTYPE-ENUM-CONTRACT-CLEANUP-01 — el payload sigue siendo string ("Freight"/
        // "OtherCost", sin cambios frontend); se convierte acá a PurchaseCostType con un TryParse
        // explícito y case-sensitive (no Enum.Parse sin control, no dejar que el model binder de
        // MediatR/JSON falle con un error genérico si algún día CostType llegara mal tipado).
        if (!Enum.TryParse<PurchaseCostType>(request.CostType, ignoreCase: false, out var costType))
            return this.ApiBadRequest(
                $"El tipo de costo '{request.CostType}' no es válido. Valores permitidos: Freight, OtherCost."
            );

        return this.ToOkOrBadRequest(
            await _mediator.Send(
                new DistributePurchaseCostCommand(
                    id,
                    costType,
                    request.Amount,
                    request.IncludedLineIds
                ),
                ct
            )
        );
    }

    [HttpPost("{id:guid}/load-pvp")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> LoadPvpSnapshots(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new LoadPvpSnapshotsCommand(id), ct));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> ConfirmPurchase(
        Guid id,
        [FromBody] ConfirmPurchaseRequest? request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ConfirmPurchaseCommand(id, request?.Schedule, request?.Retention),
                ct
            )
        );

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> CancelPurchase(
        Guid id,
        [FromBody] CancelPurchaseRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CancelPurchaseCommand(id, request.Reason, request.RequestSriAnnulment),
                ct
            )
        );

    /// <summary>Contexto completo de un ítem para el detalle de compra (1 request SSOT).</summary>
    [HttpGet("items/context")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseItemContextDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetItemContext(
        [FromQuery] Guid itemId,
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid? supplierId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchaseItemContextQuery(itemId, warehouseId, supplierId),
                ct
            )
        );

    [HttpPost("{id:guid}/lines/{lineId:guid}/update-pvp")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> UpdateLinePvp(
        Guid id,
        Guid lineId,
        [FromBody] UpdatePvpRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new UpdateLinePvpCommand(id, lineId, request.NewPvp), ct)
        );

    // ══════════════════════════════════════════════════════════════════════
    // RETENCIONES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vista previa de la retención de una compra en BORRADOR (elegibilidad + montos propuestos).
    /// ZH-PURCHASE-RETENTION-CONFIRM-01: la retención se emite dentro de <c>POST {id}/confirm</c>
    /// (<see cref="ConfirmPurchaseRequest.Retention"/>), no existe una emisión posterior.
    /// </summary>
    [HttpGet("{id:guid}/retention-preview")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetRetentionPreview(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CalculateRetentionQuery(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // RetentionDocument transversal (Modules/Retentions) — única vía de retenciones para Compras
    // desde PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E. La EMISIÓN ocurre solo dentro de
    // ConfirmPurchase (ZH-PURCHASE-RETENTION-CONFIRM-01) y la ANULACIÓN solo como consecuencia de
    // anular la compra (CancelPurchase → RetentionCanceller, ZH-RETENTION-CANCELLATION-LIFECYCLE-01:
    // la retención forma parte de la confirmación y Cancelled es terminal). Aquí queda solo la
    // lectura, que reutiliza GetRetentionBySourceQuery fijando SourceDocumentType=PurchaseInvoice y
    // SourceDocumentId=id desde la RUTA (nunca desde el body) con la policy de Compras.

    /// <summary>
    /// Retención transversal (<c>RetentionDocument</c>) activa sobre esta compra, si existe.
    /// <c>Success(null)</c> es un estado normal (todavía no se emitió ninguna), nunca un error.
    /// </summary>
    [HttpGet("{id:guid}/retention")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetRetention(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetRetentionBySourceQuery(RetentionSourceDocumentType.PurchaseInvoice, id),
                ct
            )
        );

    // ══════════════════════════════════════════════════════════════════════
    // RESUMEN FISCAL POR IMPUESTO (FLOW-READY-02D.1)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resumen fiscal persistido de la factura, agrupado por combinación de impuesto
    /// (VatCode/VatRate/IceCode/IceRate) — generado exclusivamente al confirmar la compra desde las
    /// líneas ya congeladas, nunca recalculado desde catálogos vivos. Vacío si la factura sigue en
    /// borrador (aún no confirmada).
    /// </summary>
    /// <response code="200">Resumen fiscal de la factura.</response>
    /// <response code="404">La factura no existe.</response>
    [HttpGet("/api/v1/purchases/invoices/{invoiceId:guid}/tax-summaries")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaxSummaries(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetPurchaseInvoiceTaxSummariesQuery(invoiceId), ct)
        );
}

public record UpdatePvpRequest(decimal NewPvp);

public record ApplyDiscountRequest(decimal DiscountPct);

public record DistributeCostRequest(string CostType, decimal Amount, List<Guid> IncludedLineIds);

/// <summary>
/// <see cref="RequestSriAnnulment"/> (ZH-RETENTION-SRI-ANNULMENT-01): si la retención de la compra ya
/// está AUTORIZADA, <c>true</c> inicia la anulación ante el SRI (la compra sigue confirmada hasta que el
/// SRI confirme ANULADO) en lugar de responder <c>ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT</c>.
/// </summary>
public record CancelPurchaseRequest(string Reason, bool RequestSriAnnulment = false);

/// <summary>
/// <see cref="Retention"/>: ZH-PURCHASE-RETENTION-CONFIRM-01 — intención opcional de emitir la
/// retención en esta misma confirmación (mismo contrato <see cref="RetentionIntent"/> que Gastos).
/// Ausente o null confirma exactamente igual que antes, sin retención.
/// </summary>
public record ConfirmPurchaseRequest(
    List<ConfirmScheduleInput>? Schedule = null,
    RetentionIntent? Retention = null
);
