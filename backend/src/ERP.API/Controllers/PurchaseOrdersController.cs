using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Contracts.Purchasing;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.ApprovePurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.CancelPurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.SendPurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.GetPurchaseOrderById;
using ERP.Application.Modules.Purchasing.UseCases.GetPurchaseOrdersList;
using ERP.Application.Modules.Purchasing.UseCases.GetOrdersPendingBilling;
using ERP.Application.Modules.Purchasing.UseCases.LinkInvoiceToPurchaseOrder;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Órdenes de compra", "perm:purchases.orders.view", "📝", "/purchases/orders", "perm:purchases.invoices.view", 44)]
[ApiController]
[Route("api/purchases/orders")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseOrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:purchases.orders.view")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrdersPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var (pageNumber, pageSize) = ListQueryParameters.ParsePaged(Request.Query);
        var proveedorId = ListQueryParameters.ParseOptionalGuid(Request.Query, "proveedorId");
        var status = ListQueryParameters.ParseOptionalString(Request.Query, "estado");
        var (desde, hasta) = ListQueryParameters.ParseDateTimeRange(Request.Query, "fechaDesde", "fechaHasta");

        var result = await _mediator.Send(
            new GetPurchaseOrdersListQuery(pageNumber, pageSize, proveedorId, status, desde, hasta), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("pendientes-por-facturar")]
    [Authorize(Policy = "perm:purchases.orders.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingBilling(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdersPendingBillingQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:purchases.orders.view")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPurchaseOrderByIdQuery(id), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost]
    [Authorize(Policy = "perm:purchases.orders.create")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseOrderCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPatch("{id:guid}/enviar")]
    [Authorize(Policy = "perm:purchases.orders.send")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SendOrderPurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Enviada");
    }

    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:purchases.orders.approve")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ApproveOrderPurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Aprobada");
    }

    [HttpPatch("{id:guid}/cancelar")]
    [Authorize(Policy = "perm:purchases.orders.cancel")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelOrderPurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Cancelada");
    }

    [HttpPost("{id:guid}/vincular-factura")]
    [Authorize(Policy = "perm:purchases.orders.link-invoice")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkInvoice(
        Guid id,
        [FromBody] LinkInvoiceRequest body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(id, body.PurchaseInvoiceId), ct);
        return this.ToOkOrBadRequest(result, "Factura vinculada");
    }
}
