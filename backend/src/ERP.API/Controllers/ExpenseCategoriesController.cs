using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Categories;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Categorías de gastos",
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

    /// <summary>Devuelve el árbol de categorías de gasto de la empresa activa.</summary>
    /// <remarks>
    /// Jerarquía de tres niveles: tipo de gasto → categoría → subcategoría. Los hermanos se ordenan por código y
    /// nombre. Las rutas <c>GET /api/v1/expenses/categories</c> y <c>GET /api/v1/expenses/categories/tree</c> son
    /// equivalentes.
    /// </remarks>
    /// <param name="includeInactive">Incluye también los nodos desactivados (por defecto <c>false</c>).</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Árbol de categorías de gasto.</response>
    [HttpGet]
    [HttpGet("tree")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseCategoryTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ListExpenseCategoryTreeQuery(includeInactive), ct)
        );

    /// <summary>Obtiene un nodo del catálogo de categorías de gasto.</summary>
    /// <param name="id">Id del nodo (tipo, categoría o subcategoría).</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo de categoría de gasto.</response>
    /// <response code="404">El nodo no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogView}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetExpenseCategoryNodeByIdQuery(id), ct));

    /// <summary>Crea un nodo del catálogo de categorías de gasto.</summary>
    /// <remarks>
    /// El nivel (<c>level</c>) fija el lugar en la jerarquía: un tipo no tiene padre, una categoría cuelga de un tipo y
    /// una subcategoría cuelga de una categoría. El padre debe existir y estar activo. Solo las subcategorías llevan
    /// cuenta contable, y es obligatoria: debe existir, estar activa, ser de tipo gasto y permitir movimientos.
    /// El código y el nombre deben ser únicos entre los nodos del mismo nivel y padre.
    /// </remarks>
    /// <param name="cmd">Datos del nodo: <c>code</c>, <c>name</c>, <c>level</c>, <c>parentId</c>, <c>accountingAccountId</c>, <c>description</c>, <c>isDeductible</c> y <c>requiresInvoice</c>.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Nodo creado.</response>
    /// <response code="404">El nodo padre o la cuenta contable indicada no existen.</response>
    /// <response code="409">Ya existe un nodo con el mismo código o nombre en ese nivel.</response>
    /// <response code="422">Jerarquía o cuenta contable inválidas (padre inactivo, nivel incorrecto, cuenta ausente o no válida) o datos inválidos.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogCreate}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryNodeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateExpenseCategoryNodeCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    /// <summary>Actualiza el código, nombre, descripción y cuenta contable de un nodo.</summary>
    /// <remarks>
    /// No cambia el nivel ni el padre del nodo. La cuenta contable solo aplica a subcategorías (donde es obligatoria y
    /// debe ser una cuenta de gasto activa que permita movimientos). Este endpoint no recibe <c>isDeductible</c> ni
    /// <c>requiresInvoice</c>: en una subcategoría ambos quedan en <c>true</c>.
    /// </remarks>
    /// <param name="id">Id del nodo.</param>
    /// <param name="body">Código, nombre, cuenta contable y descripción.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo actualizado.</response>
    /// <response code="404">El nodo o la cuenta contable indicada no existen.</response>
    /// <response code="409">Ya existe otro nodo con el mismo código o nombre en ese nivel.</response>
    /// <response code="422">Cuenta contable ausente o no válida, cuenta en un nodo que no es subcategoría, o datos inválidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>Activa un nodo del catálogo de categorías de gasto.</summary>
    /// <remarks>
    /// Baja y alta son lógicas: los nodos no se eliminan. Para activar un nodo con padre, el padre debe estar activo.
    /// Disponible como <c>POST</c> y como <c>PATCH</c> con el mismo efecto.
    /// </remarks>
    /// <param name="id">Id del nodo.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo activado.</response>
    /// <response code="404">El nodo o su padre no existen.</response>
    /// <response code="422">El padre del nodo está inactivo.</response>
    [HttpPost("{id:guid}/activate")]
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogActivate}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ActivateExpenseCategoryNodeCommand(id), ct));

    /// <summary>Desactiva un nodo del catálogo de categorías de gasto.</summary>
    /// <remarks>
    /// Baja lógica: el nodo no se elimina. No se puede desactivar un nodo que tenga hijos activos.
    /// Disponible como <c>POST</c> y como <c>PATCH</c> con el mismo efecto.
    /// </remarks>
    /// <param name="id">Id del nodo.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Nodo desactivado.</response>
    /// <response code="404">El nodo no existe.</response>
    /// <response code="422">El nodo tiene hijos activos.</response>
    [HttpPost("{id:guid}/deactivate")]
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = $"perm:{ExpensePermissions.CatalogDeactivate}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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
