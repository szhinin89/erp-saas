using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Contracts.Sales;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Application.Modules.Commercial.UseCases.CancelSalesOrder;
using ERP.Application.Modules.Commercial.UseCases.ConfirmSalesOrder;
using ERP.Application.Modules.Commercial.UseCases.ConvertQuoteToOrder;
using ERP.Application.Modules.Commercial.UseCases.CreateSalesOrder;
using ERP.Application.Modules.Commercial.UseCases.GetSalesOrderByPublicId;
using ERP.Application.Modules.Commercial.UseCases.GetSalesOrdersList;
using ERP.Application.Sales.UseCases.CreateSale;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Ventas", Permissions.SalesOrder.View, "📦", "/sales/orders", null, 46)]
[ApiController]
[Route("api/sales/orders")]
[Authorize]
[Produces("application/json")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesOrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:sales.orders.view")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrdersPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var (pageNumber, pageSize) = ListQueryParameters.ParsePaged(Request.Query);
        var businessPartnerId = ListQueryParameters.ParseOptionalGuid(Request.Query, "businessPartnerId");
        var status = ListQueryParameters.ParseOptionalString(Request.Query, "status");
        var (dateFrom, dateTo) = ListQueryParameters.ParseDateOnlyRange(Request.Query);

        var result = await _mediator.Send(
            new GetSalesOrdersListQuery(pageNumber, pageSize, businessPartnerId, status, dateFrom, dateTo),
            ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("{publicId:guid}")]
    [Authorize(Policy = "perm:sales.orders.view")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderDetailDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPublicId(Guid publicId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSalesOrderByPublicIdQuery(publicId), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost]
    [Authorize(Policy = "perm:sales.orders.create")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderRequest request, CancellationToken ct)
    {
        var command = new CreateSalesOrderCommand(
            request.BusinessPartnerId,
            request.WarehouseId,
            request.RequiredDate,
            request.PaymentTermDays,
            request.Notes,
            request.Lines.Select(l => new SalesOrderLineInputDto(
                l.ProductId, l.Quantity, l.UnitPrice, l.TaxRatePct)).ToList());

        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPost("{publicId:guid}/confirm")]
    [Authorize(Policy = "perm:sales.orders.update")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid publicId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmSalesOrderCommand(publicId), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("{publicId:guid}/cancel")]
    [Authorize(Policy = "perm:sales.orders.update")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid publicId, [FromBody] CancelSalesOrderRequest? request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelSalesOrderCommand(publicId, request?.Reason), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("from-quote")]
    [Authorize(Policy = "perm:sales.orders.create")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> ConvertFromQuote([FromBody] ConvertQuoteToOrderRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ConvertQuoteToOrderCommand(request.QuotePublicId, request.WarehouseId, request.RequiredDate),
            ct);
        return this.ToCreatedOrBadRequest(result, "Convertido");
    }

    /// <summary>Crea salesBill borrador desde un pedido confirmado (ORDER_TO_INVOICE).</summary>
    [HttpPost("{publicId:guid}/invoice")]
    [Authorize(Policy = "perm:sales.invoices.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvoice(Guid publicId, [FromBody] InvoiceFromOrderRequest? request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateSaleCommand(
                BusinessPartnerId: Guid.Empty,
                WarehouseId: Guid.Empty,
                BranchId: Guid.Empty,
                Items: [],
                PaymentMethodCode: request?.PaymentMethodCode ?? "01",
                PaymentDays: request?.PaymentDays ?? 0,
                Notes: request?.Notes,
                SalesOrderPublicId: publicId),
            ct);
        return this.ToCreatedOrBadRequest(result, "salesBill creada");
    }
}
