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
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchaseListQuery(search, status, pageNumber, pageSize),
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
            await _mediator.Send(new ConfirmPurchaseCommand(id, request?.Schedule), ct)
        );

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> CancelPurchase(
        Guid id,
        [FromBody] CancelPurchaseRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CancelPurchaseCommand(id, request.Reason), ct)
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

    [HttpGet("{id:guid}/retention-preview")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetRetentionPreview(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CalculateRetentionQuery(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // RetentionDocument transversal (Modules/Retentions) — única vía de retenciones para Compras
    // desde PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E. Reutiliza IssueRetentionCommand/
    // GetRetentionBySourceQuery/CancelRetentionCommand (transversales, ya generalizados en
    // PURCHASES-RETENTIONS-BRIDGE-05B/PURCHASES-RETENTIONS-CANCEL-05D) — este controller no
    // duplica ninguna lógica de emisión/elegibilidad/CxP, solo fija SourceDocumentType=
    // PurchaseInvoice y SourceDocumentId=id desde la RUTA (nunca desde el body, mismo criterio de
    // seguridad que el resto del ERP: el body es un hint de UX, nunca autoridad).
    //
    // Se decidió NO crear un endpoint transversal en RetentionsController (p. ej.
    // "POST /api/v1/retentions/issue"): el permiso requerido difiere por SourceDocumentType
    // (PurchasePermissions.Update para Compras) y este ERP no tiene todavía un mecanismo de policy
    // dinámica por contenido del body — [Authorize(Policy=...)] se resuelve antes del binding del
    // command. Mantener el endpoint aquí, en PurchasesController, reutiliza la misma policy sin
    // crear un controller propio de Retenciones para Compras (la lógica de negocio sigue siendo
    // 100% transversal — ver IssueRetentionCommand/RetentionIssuer).

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

    /// <summary>
    /// Emite una retención transversal (<c>RetentionDocument</c>) sobre esta compra confirmada. El
    /// número de retención NUNCA viaja en el body (se genera server-side vía
    /// <c>DocumentSequence.CaptureNextAsync</c>, mismo criterio que el resto del ERP) y
    /// <c>SourceDocumentId</c> siempre es <paramref name="id"/> de la ruta, nunca un valor del body.
    /// </summary>
    [HttpPost("{id:guid}/retention")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> IssueRetention(
        Guid id,
        [FromBody] IssuePurchaseRetentionRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new IssueRetentionCommand(
                    RetentionSourceDocumentType.PurchaseInvoice,
                    id,
                    request.EmissionPointId,
                    request.IssueDate,
                    request.Lines
                ),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// PURCHASES-RETENTIONS-CANCEL-05D — anula la retención transversal (<c>RetentionDocument</c>)
    /// de esta compra: reversa la <c>AccountsPayable</c> ya reducida al emitirla (si tenía monto
    /// retenido real) y el asiento contable original (vía <c>RetentionDocumentCancelledPostingTranslator</c>,
    /// genérico, sin cambios). Reutiliza <see cref="CancelRetentionCommand"/> tal cual — este
    /// controller solo valida que <paramref name="retentionId"/> sea realmente la retención activa
    /// de <paramref name="purchaseId"/> (nunca confía en que el cliente no se equivocó de Id) antes
    /// de delegar.
    /// </summary>
    [HttpPost("{purchaseId:guid}/retention/{retentionId:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> CancelRetention(
        Guid purchaseId,
        Guid retentionId,
        [FromBody] CancelPurchaseRetentionRequest request,
        CancellationToken ct
    )
    {
        var existing = await _mediator.Send(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.PurchaseInvoice, purchaseId),
            ct
        );
        if (!existing.IsSuccess)
            return this.ToOkOrBadRequest(existing);
        if (existing.Value is null || existing.Value.Id != retentionId)
            return this.ApiNotFound("La retención no existe o no pertenece a esta compra.");

        return this.ToOkOrBadRequest(
            await _mediator.Send(new CancelRetentionCommand(retentionId, request.Reason), ct),
            "OK"
        );
    }

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

/// <summary>PURCHASES-RETENTIONS-UI-MIGRATION-05C — nunca incluye SourceDocumentType/SourceDocumentId (fijos por la ruta) ni un número de retención manual.</summary>
public record IssuePurchaseRetentionRequest(
    Guid EmissionPointId,
    DateOnly IssueDate,
    IReadOnlyList<IssueRetentionLineInput> Lines
);

/// <summary>PURCHASES-RETENTIONS-CANCEL-05D.</summary>
public record CancelPurchaseRetentionRequest(string Reason);

public record CancelPurchaseRequest(string Reason);

public record ConfirmPurchaseRequest(List<ConfirmScheduleInput>? Schedule = null);
