using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Sales.DTOs;
using ERP.Application.Sales.UseCases.VoidInvoice;
using ERP.Application.Sales.UseCases.CreateSale;
using ERP.Application.Sales.UseCases.IssueElectronicInvoice;
using ERP.Application.Sales.UseCases.GetAvailableStockForSale;
using ERP.Application.Sales.UseCases.GetSaleById;
using ERP.Application.Sales.UseCases.GetSalesList;
using ERP.Application.Sales.UseCases.RetrySubmission;
using ERP.Application.Sales.UseCases.ValidateSale;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Facturación electrónica SRI — Borrador → Validado → Autorizado.</summary>
[AppFeature("Ventas", "perm:sales.invoices.view", "🧾", "/sales/invoices", null, 50)]
[ApiController]
[Route("api/sales/invoices")]
[Authorize]
[Produces("application/json")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:sales.invoices.view")]
    [ProducesResponseType(typeof(ApiResponse<SalesPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var (pageNumber, pageSize) = ListQueryParameters.ParsePaged(Request.Query);
        var customerId = ListQueryParameters.ParseOptionalGuid(Request.Query, "clienteId");
        var (desde, hasta) = ListQueryParameters.ParseDateTimeRange(Request.Query);
        var status = ListQueryParameters.ParseOptionalString(Request.Query, "estado");
        var search = CatalogQueryParameters.ParseSearch(Request.Query);

        var result = await _mediator.Send(
            new GetSalesListQuery(pageNumber, pageSize, customerId, desde, hasta, status, search),
            ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:sales.invoices.view")]
    [ProducesResponseType(typeof(ApiResponse<SalesBillDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSaleByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpGet("stock")]
    [Authorize(Policy = "perm:ventas.stock.view")]
    [ProducesResponseType(typeof(ApiResponse<AvailableStockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStock(CancellationToken ct = default)
    {
        if (!Request.Query.TryGetValue("productoId", out var pv) || !Guid.TryParse(pv, out var productoId))
            return this.ApiBadRequest("El parámetro productoId es requerido y debe ser un GUID válido.");
        if (!Request.Query.TryGetValue("bodegaId", out var bv) || !Guid.TryParse(bv, out var bodegaId))
            return this.ApiBadRequest("El parámetro bodegaId es requerido y debe ser un GUID válido.");

        var result = await _mediator.Send(new GetAvailableStockForSaleQuery(productoId, bodegaId), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost]
    [Authorize(Policy = "perm:sales.invoices.create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSaleCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPatch("{id:guid}/validar")]
    [Authorize(Policy = "perm:sales.invoices.update")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidateSaleCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Validado");
    }

    [HttpPatch("{id:guid}/emitir")]
    [Authorize(Policy = "perm:sales.invoices.update")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Emit(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new IssueElectronicInvoiceCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Emitido");
    }

    [HttpPatch("{id:guid}/reintentar")]
    [Authorize(Policy = "perm:sales.invoices.update")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RetrySubmissionCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Reintentado");
    }

    [HttpPatch("{id:guid}/anular")]
    [Authorize(Policy = "perm:sales.invoices.void")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new VoidInvoiceCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Anulado");
    }
}
