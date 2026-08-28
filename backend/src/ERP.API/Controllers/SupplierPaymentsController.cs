using ERP.API.Extensions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — API del módulo independiente de Pagos a Proveedores
/// (<c>SupplierPayment</c>), aprobado por SUPPLIER-PAYMENTS-AUDIT-15A. Sin Draft: el único endpoint
/// de escritura registra y confirma el pago en una sola operación. No es <c>/api/v1/finance/payments</c>
/// (legacy, eliminado en PAYABLES-PAYMENTS-LEGACY-CLEANUP-14) ni afecta <c>Payment</c>/
/// <c>PaymentApplicationLine</c> (Collections/CxC).
/// </summary>
[ApiController]
[Route("api/v1/supplier-payments")]
[Authorize]
[Produces("application/json")]
public sealed class SupplierPaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupplierPaymentsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterSupplierPaymentRequest body,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new RegisterSupplierPaymentCommand(
                    body.SupplierId,
                    body.PaymentDate,
                    body.TotalAmount,
                    body.ReceiptNumber,
                    body.MethodLines,
                    body.ApplicationLines,
                    body.Allocations
                ),
                ct
            )
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetSupplierPaymentByIdQuery(id), ct));

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Reverse}")]
    public async Task<IActionResult> Reverse(
        Guid id,
        [FromBody] ReverseSupplierPaymentRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ReverseSupplierPaymentCommand(id, body.Reason), ct)
        );

    [HttpGet]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSupplierPaymentsListQuery(supplierId, status, page, pageSize), ct),
            "OK"
        );
}
