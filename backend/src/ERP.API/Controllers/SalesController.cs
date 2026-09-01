using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Modules.Sales.UseCases.GetDailySalesReport;
using ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;
using ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;
using ERP.Application.Modules.Sales.UseCases.GetSalesReceiptPrintPayload;
using ERP.Application.Modules.Sales.UseCases.GetSalesRuntimeContext;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Ventas", $"perm:{SalesPermissions.View}", "💰", "/sales", null, 65)]
[ApiController]
[Route("api/v1/sales")]
[Authorize]
[Produces("application/json")]
public sealed class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = $"perm:{SalesPermissions.Create}")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateSalesDraftCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdateSalesDraftCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetSalesInvoiceByIdQuery(id), ct));

    [HttpGet("invoices/{invoiceId:guid}/receipt-print-payload")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetReceiptPrintPayload(
        Guid invoiceId,
        CancellationToken ct
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetSalesReceiptPrintPayloadQuery(invoiceId), ct)
        );

    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetSalesInvoiceListQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Reporte básico de ventas por rango de fechas (piloto Sumak). Sin fechas, usa el día
    /// actual (UTC). Company-scoped — ver GetDailySalesReportQuery.
    /// </summary>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetDailyReport(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetDailySalesReportQuery(dateFrom, dateTo), ct),
            "OK"
        );

    [HttpPost("{id:guid}/apply-discount")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    public async Task<IActionResult> ApplyGlobalDiscount(
        Guid id,
        [FromBody] SalesApplyDiscountRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ApplySalesDiscountCommand(id, request.DiscountPct), ct)
        );

    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    public async Task<IActionResult> AuthorizeInvoice(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new AuthorizeSalesInvoiceCommand(id), ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    public async Task<IActionResult> CancelInvoice(
        Guid id,
        [FromBody] CancelSalesRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CancelSalesInvoiceCommand(id, request.Reason), ct)
        );

    /// <summary>
    /// Devuelve los valores por defecto para inicializar una nueva factura de venta.
    /// Requiere solo autenticación — cualquier usuario con acceso a ventas puede leer esta configuración.
    /// </summary>
    [HttpGet("invoice-defaults")]
    [Authorize]
    public async Task<IActionResult> GetInvoiceDefaults(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSalesInvoiceDefaultsQuery(), ct));

    /// <summary>
    /// Contexto agregado para Ventas y futuras pantallas (POS, facturación rápida, pedidos, app
    /// móvil): política fiscal de Consumidor Final + defaults de factura, en un solo GET.
    /// Requiere solo autenticación — mismo criterio que invoice-defaults.
    /// </summary>
    [HttpGet("runtime-context")]
    [Authorize]
    public async Task<IActionResult> GetRuntimeContext(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSalesRuntimeContextQuery(), ct));

    [HttpGet("item-search")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> SearchItems(
        [FromQuery] string? q,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SearchItemsForInvoiceQuery(q ?? string.Empty, warehouseId, pageSize),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Precio e impuestos oficiales de un ítem, resueltos vía el Pricing Engine v2
    /// (IPricingResolver) — se invoca en el momento puntual en que el usuario
    /// selecciona un producto para agregarlo como línea de venta.
    /// </summary>
    [HttpGet("items/{itemId:guid}/pricing")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetItemPricing(Guid itemId, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSalesItemPricingQuery(itemId), ct));
}

public record SalesApplyDiscountRequest(decimal DiscountPct);

public record CancelSalesRequest(string Reason);
