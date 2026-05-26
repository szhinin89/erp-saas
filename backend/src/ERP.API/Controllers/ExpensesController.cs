using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Contracts.Expenses;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.ApproveExpense;
using ERP.Application.Modules.Expenses.UseCases.CreateExpense;
using ERP.Application.Modules.Expenses.UseCases.GetExpenseById;
using ERP.Application.Modules.Expenses.UseCases.GetExpenses;
using ERP.Application.Modules.Expenses.UseCases.RejectExpense;
using ERP.Application.Modules.Expenses.UseCases.ValidateExpense;
using ERP.Domain.Modules.Expenses.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Gastos", "perm:expenses.invoices.view", "💸", "/expenses", null, 55)]
[ApiController]
[Route("api/expenses")]
[Authorize]
[Produces("application/json")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpensesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:expenses.invoices.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        ExpenseStatus? estado = null;
        if (Request.Query.TryGetValue("estado", out var ev) &&
            Enum.TryParse<ExpenseStatus>(ev, out var ep))
            estado = ep;

        var proveedorId = ListQueryParameters.ParseOptionalGuid(Request.Query, "proveedorId");
        var (desde, hasta) = ListQueryParameters.ParseDateTimeRange(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);

        var result = await _mediator.Send(
            new GetExpensesQuery(estado, proveedorId, desde, hasta, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ExpenseInvoiceDto>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:expenses.invoices.view")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetExpenseByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost("manual")]
    [Authorize(Policy = "perm:expenses.invoices.create")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateManual(
        [FromBody] CreateExpenseCommand command,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(command with { Modo = ExpenseCreationMode.Manual }, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPost("xml")]
    [Authorize(Policy = "perm:expenses.invoices.create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromXml(
        IFormFile xmlFile,
        [FromForm] string categoriaGasto,
        [FromForm] string? observaciones,
        CancellationToken ct = default)
    {
        if (xmlFile is null || xmlFile.Length == 0)
            return this.ApiBadRequest("Debe adjuntar el archivo XML del comprobante.");

        await using var ms = new MemoryStream();
        await xmlFile.CopyToAsync(ms, ct);

        var result = await _mediator.Send(
            new CreateExpenseCommand(
                Modo: ExpenseCreationMode.Xml,
                XmlContent: ms.ToArray(),
                XmlFileName: xmlFile.FileName,
                BusinessPartnerId: null,
                IssueDate: null,
                Concept: null,
                Category: categoriaGasto,
                Subtotal: null,
                VatTotal: null,
                Total: null,
                Notes: observaciones),
            ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPatch("{id:guid}/validar")]
    [Authorize(Policy = "perm:expenses.invoices.validate")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ValidateExpenseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Validado");
    }

    [HttpPatch("{id:guid}/aprobar")]
    [Authorize(Policy = "perm:expenses.invoices.approve")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ApproveExpenseCommand(id), ct);
        return this.ToOkOrBadRequest(result, "IsApproved");
    }

    [HttpPatch("{id:guid}/rechazar")]
    [Authorize(Policy = "perm:expenses.invoices.reject")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectExpenseRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RejectExpenseCommand(id, request.Reason), ct);
        return this.ToOkOrBadRequest(result, "Rechazado");
    }
}
