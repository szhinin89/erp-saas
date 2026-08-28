using ERP.API.Extensions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// PAYABLES-READ-API-11 — API de solo lectura sobre la CxP genérica (<c>AccountsPayable</c>),
/// única fuente viva de saldo para Compras y Gastos desde PAYABLES-PURCHASE-MIGRATION-10. Sin
/// endpoints de escritura: no hay abonos/pagos aquí (eso queda para un ticket futuro de
/// PagoCabecera/PagoDetalle) — mismo patrón de solo-lectura que <c>PurchasePayablesController</c>/
/// <c>SalesReceivablesController</c>. Sin <c>[AppFeature]</c>/<c>[NavItem]</c> deliberadamente:
/// todavía no existe una pantalla propia para esta API transversal.
/// </summary>
[ApiController]
[Route("api/v1/payables")]
[Authorize]
[Produces("application/json")]
public sealed class PayablesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayablesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PayablesPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountsPayableByIdQuery(id), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{PayablesPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? originType = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? dueDateFrom = null,
        [FromQuery] DateOnly? dueDateTo = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetAccountsPayablesListQuery(
                    supplierId,
                    originType,
                    status,
                    dueDateFrom,
                    dueDateTo,
                    search,
                    page,
                    pageSize
                ),
                ct
            ),
            "OK"
        );
}
