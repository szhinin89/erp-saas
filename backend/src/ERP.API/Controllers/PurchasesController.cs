using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Purchases.UseCases.GetPurchaseItemContext;
using ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;
using ERP.Domain.Kernel.Permissions;
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

    [HttpGet("{id:guid}/withholding")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetWithholdingByPurchase(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetWithholdingByPurchaseQuery(id), ct));

    [HttpPost("{id:guid}/withholding")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> IssueWithholding(
        Guid id,
        [FromBody] IssueWithholdingRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new IssueWithholdingCommand(id, request.EmissionPointId, request.IssueDate),
                ct
            )
        );

    [HttpGet("withholdings/{whId:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetWithholdingById(Guid whId, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetWithholdingByIdQuery(whId), ct));

    [HttpPost("withholdings/{whId:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    public async Task<IActionResult> CancelWithholding(
        Guid whId,
        [FromBody] CancelWithholdingRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CancelWithholdingCommand(whId, request.Reason), ct)
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

public record IssueWithholdingRequest(Guid EmissionPointId, DateOnly IssueDate);

public record CancelWithholdingRequest(string Reason);

public record CancelPurchaseRequest(string Reason);

public record ConfirmPurchaseRequest(List<ConfirmScheduleInput>? Schedule = null);
