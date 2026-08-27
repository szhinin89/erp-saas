using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.UseCases.Categories;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Expenses.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Catalogo de Gastos",
    $"perm:{ExpensePermissions.CatalogView}",
    "ReceiptText",
    "/expenses/categories",
    null,
    47,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/expenses/categories")]
[Authorize]
[Produces("application/json")]
public sealed class ExpenseCategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpenseCategoriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HttpGet("tree")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogView}")]
    public async Task<IActionResult> GetTree(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ListExpenseCategoryTreeQuery(includeInactive), ct)
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetExpenseCategoryNodeByIdQuery(id), ct)
        );

    [HttpPost]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogCreate}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateExpenseCategoryNodeCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogUpdate}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateExpenseCategoryNodeRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateExpenseCategoryNodeCommand(
                    id,
                    body.Code,
                    body.Name,
                    body.AccountingAccountId,
                    body.Description
                ),
                ct
            )
        );

    [HttpPost("{id:guid}/activate")]
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogActivate}")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ActivateExpenseCategoryNodeCommand(id), ct)
        );

    [HttpPost("{id:guid}/deactivate")]
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogDeactivate}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DeactivateExpenseCategoryNodeCommand(id), ct)
        );
}

public sealed record UpdateExpenseCategoryNodeRequest(
    string Code,
    string Name,
    Guid? AccountingAccountId,
    string? Description = null
);
