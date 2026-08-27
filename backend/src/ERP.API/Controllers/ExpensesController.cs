using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Documentos de Gastos",
    $"perm:{ExpensePermissions.DocumentsView}",
    "ReceiptText",
    "/expenses/documents",
    $"perm:{ExpensePermissions.CatalogView}",
    50,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/expenses/documents")]
[Authorize]
[Produces("application/json")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpensesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ListExpenseDocumentsQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetExpenseDocumentByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsCreate}")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateExpenseDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateExpenseDraftCommand(
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes
                ),
                ct
            )
        );

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsUpdate}")]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdateExpenseDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateExpenseDraftCommand(
                    id,
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes
                ),
                ct
            )
        );

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsConfirm}")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ConfirmExpenseDocumentCommand(id), ct));
}
