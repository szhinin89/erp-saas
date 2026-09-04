using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Retentions.Enums;
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
                    request.Notes,
                    request.TaxSupportCode
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
                    request.Notes,
                    request.TaxSupportCode
                ),
                ct
            )
        );

    /// <summary>
    /// RETENTIONS-ELIGIBILITY-01 — endpoint delgado (solo delega al mediator), de solo lectura.
    /// Reutiliza el permiso de lectura ya existente de Gastos (<see cref="ExpensePermissions.DocumentsView"/>)
    /// porque Retentions todavía no tiene su propio catálogo de permisos finos — eso queda para
    /// E1-C (ver RETENTIONS-MODULE-DESIGN-01.md), que agrega `retentions.view`/`retentions.issue`/
    /// `retentions.cancel`. No crea ni emite ningún <c>RetentionDocument</c> — esa entidad no
    /// existe todavía en esta subfase.
    /// </summary>
    [HttpGet("{id:guid}/retention-eligibility")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    public async Task<IActionResult> GetRetentionEligibility(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, id),
                ct
            )
        );

    /// <summary>
    /// RETENTIONS-API-EXPENSES-01E — <paramref name="request"/> es opcional (sin body, o con
    /// <c>Retention</c> null, preserva el comportamiento previo a esta fase: confirma sin
    /// retención). El mapeo a <see cref="RetentionIntent"/> es 1:1, sin lógica de negocio en el
    /// controller — la elegibilidad y el cálculo siguen resolviéndose en
    /// <c>ConfirmExpenseDocumentHandler</c>.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsConfirm}")]
    public async Task<IActionResult> Confirm(
        Guid id,
        [FromBody] ConfirmExpenseDocumentRequest? request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ConfirmExpenseDocumentCommand(id, ToRetentionIntent(request?.Retention)),
                ct
            )
        );

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsCancel}")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelExpenseDocumentRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CancelExpenseDocumentCommand(id, request.Reason), ct)
        );

    [HttpPost("confirmed")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsConfirm}")]
    public async Task<IActionResult> CreateConfirmed(
        [FromBody] CreateExpenseDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateConfirmedExpenseCommand(
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
                    request.Notes,
                    request.TaxSupportCode,
                    ToRetentionIntent(request.Retention)
                ),
                ct
            )
        );

    /// <summary>
    /// RETENTIONS-API-EXPENSES-01E — retención activa (<c>Status != Cancelled</c>) asociada al
    /// gasto, si existe. <see cref="GetRetentionBySourceQuery"/> devuelve
    /// <c>Success(null)</c> cuando no hay retención (nunca <c>NotFound</c> — "no existe" es un
    /// estado normal, no un error), por lo que el 404 lo decide este endpoint, no el handler.
    /// Mismo permiso de solo lectura que <see cref="GetRetentionEligibility"/>
    /// (<see cref="ExpensePermissions.DocumentsView"/>) — Retentions no tiene todavía su propio
    /// catálogo de permisos (ver RETENTIONS-MODULE-DESIGN-01.md).
    /// </summary>
    [HttpGet("{id:guid}/retention")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    public async Task<IActionResult> GetRetention(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, id),
            ct
        );

        if (!result.IsSuccess)
            return this.ToOkOrBadRequest(result);

        return result.Value is null ? this.ApiNotFound() : this.ApiOk(result.Value);
    }

    /// <summary>
    /// Mapeo delgado de contrato de API (<see cref="RetentionIntentRequest"/>) a command interno
    /// (<see cref="RetentionIntent"/>) — 1:1, sin cálculo ni resolución de contexto. <c>null</c> se
    /// preserva como <c>null</c> (comportamiento actual, sin retención).
    /// </summary>
    private static RetentionIntent? ToRetentionIntent(RetentionIntentRequest? request) =>
        request is null
            ? null
            : new RetentionIntent(
                request.AppliesRetention,
                request.EmissionPointId,
                request.IssueDate,
                request.Lines?
                    .Select(l => new IssueRetentionLineInput(
                        l.TaxType,
                        l.RetentionCode,
                        l.BaseAmount,
                        l.RetentionRate,
                        l.RetainedAmount,
                        l.Description,
                        l.RetentionCodeDescription
                    ))
                    .ToList()
            );
}
