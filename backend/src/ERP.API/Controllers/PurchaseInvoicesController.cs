using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Contracts.Purchasing;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.ApprovePurchase;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchase;
using ERP.Application.Modules.Purchasing.UseCases.GetPurchaseById;
using ERP.Application.Modules.Purchasing.UseCases.GetPurchases;
using ERP.Application.Modules.Purchasing.UseCases.RejectPurchase;
using ERP.Application.Modules.Purchasing.UseCases.ValidatePurchase;
using ERP.Domain.Modules.Purchasing.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Compras", "perm:purchases.invoices.view", "🛒", "/purchases/invoices", null, 45)]
[ApiController]
[Route("api/purchases/invoices")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseInvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseInvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:purchases.invoices.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PurchBillDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        PurchaseStatus? Status = null;
        if (Request.Query.TryGetValue("estado", out var ev) &&
            Enum.TryParse<PurchaseStatus>(ev, out var ep))
            Status = ep;

        var proveedorId = ListQueryParameters.ParseOptionalGuid(Request.Query, "proveedorId");
        var (desde, hasta) = ListQueryParameters.ParseDateTimeRange(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);

        var result = await _mediator.Send(
            new GetPurchasesQuery(Status, proveedorId, desde, hasta, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<PurchBillDto>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:purchases.invoices.view")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPurchaseByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost("manual")]
    [Authorize(Policy = "perm:purchases.invoices.create")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateManual(
        [FromBody] CreatePurchaseCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command with { Mode = PurchaseCreationMode.Manual }, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPost("xml")]
    [Authorize(Policy = "perm:purchases.invoices.create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromXml(IFormFile xmlFile, CancellationToken ct = default)
    {
        if (xmlFile is null || xmlFile.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo XML de la salesBill.");

        await using var ms = new MemoryStream();
        await xmlFile.CopyToAsync(ms, ct);

        var result = await _mediator.Send(
            new CreatePurchaseCommand(
                Mode: PurchaseCreationMode.Xml,
                XmlContent: ms.ToArray(),
                XmlFileName: xmlFile.FileName,
                BusinessPartnerId: null,
                InvoiceNumber: null,
                InvoiceDate: null,
                DueDate: null,
                PaymentTerms: null,
                Notes: null,
                Lines: null,
                WarehouseAllocations: null),
            ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPatch("{id:guid}/validar")]
    [Authorize(Policy = "perm:compras.facturas.validate")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidatePurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Validado");
    }

    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:compras.facturas.approve")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ApprovePurchaseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "IsApproved");
    }

    [HttpPatch("{id:guid}/rechazar")]
    [Authorize(Policy = "perm:compras.facturas.reject")]
    [ProducesResponseType(typeof(ApiResponse<PurchBillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectPurchaseRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RejectPurchaseCommand(id, request.Reason), ct);
        return this.ToOkOrBadRequest(result, "Rechazado");
    }
}
