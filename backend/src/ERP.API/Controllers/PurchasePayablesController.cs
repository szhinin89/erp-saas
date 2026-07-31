using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — expone <c>PurchasePayable</c> (CxP) para consulta,
/// necesario para que el usuario seleccione qué deuda liquidar antes de registrar un pago vía
/// <c>FinancePaymentsController</c>. Mismo patrón que <c>SalesReceivablesController</c> (Sales).
/// Sin endpoints de escritura: la liquidación (Payment.Apply + PurchasePayable.RegisterPayment)
/// vive exclusivamente en FinancePaymentsController/RegisterPaymentCommand.
/// Sin [AppFeature] deliberadamente — ver comentario equivalente en SalesReceivablesController
/// (colisión de clave de menú con PurchasesController si se reutiliza PurchasePermissions.View).
/// </summary>
[ApiController]
[Route("api/v1/purchase-payables")]
[Authorize]
[Produces("application/json")]
public sealed class PurchasePayablesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchasePayablesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPayableByIdQuery(id), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPayablesListQuery(status, pageNumber, pageSize), ct),
            "OK"
        );
}
