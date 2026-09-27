using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-API-02E-D — solicitudes de efectivo (<c>CashFundingRequest</c>, 02E-B/C).
/// Solicitante (<c>supplier-payments.create</c>): crea, lista las suyas, ve su detalle y cancela la
/// suya pendiente. Cajero (<c>caja.funding-requests.view</c>/<c>.fulfill</c>): bandeja de la
/// sucursal activa, detalle y entregar/rechazar SOLO si controla la sesión de caja (validado en
/// Application; el permiso nunca reemplaza el ownership). Los comandos responden el detalle re-leído.
/// </summary>
[ApiController]
[Route("api/v1/cash-funding-requests")]
[Authorize]
[Produces("application/json")]
public sealed class CashFundingRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashFundingRequestsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    public async Task<IActionResult> Create([FromBody] CreateCashFundingRequestRequest body, CancellationToken ct)
    {
        var payment = new RegisterSupplierPaymentCommand(
            body.SupplierId,
            body.PaymentDate,
            body.TotalAmount,
            body.ReceiptNumber,
            body.MethodLines,
            body.ApplicationLines ?? [],
            body.Allocations ?? [],
            body.ConfirmUnappliedAmount
        );
        var result = await _mediator.Send(new CreateCashFundingRequestCommand(payment, body.ClientRequestId), ct);
        return this.ToCreatedOrBadRequest(await WithDetailAsync(result, ct));
    }

    /// <summary>Bandeja de caja: empresa operativa + sucursal activa, siempre.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.FundingRequestsView}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] Guid? requestedByUserId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetCashFundingRequestListQuery(status, cashRegisterId, requestedByUserId, page, pageSize),
                ct
            )
        );

    /// <summary>"Mis solicitudes": el solicitante es siempre el usuario autenticado.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    public async Task<IActionResult> GetMine(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) => this.ToOkOrBadRequest(await _mediator.Send(new GetMyCashFundingRequestsQuery(status, page, pageSize), ct));

    /// <summary>
    /// Solicitante O <c>caja.funding-requests.view</c>: la regla OR se resuelve en Application con el
    /// autorizador oficial (no hay políticas OR). Sin acceso → 404.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashFundingRequestByIdQuery(id), ct));

    [HttpPost("{id:guid}/fulfill")]
    [Authorize(Policy = $"perm:{CajaPermissions.FundingRequestsFulfill}")]
    public async Task<IActionResult> Fulfill(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await WithDetailAsync(await _mediator.Send(new FulfillCashFundingRequestCommand(id), ct), ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = $"perm:{CajaPermissions.FundingRequestsFulfill}")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] CashFundingRequestReasonRequest body, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await WithDetailAsync(await _mediator.Send(new RejectCashFundingRequestCommand(id, body.Reason), ct), ct)
        );

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CashFundingRequestReasonRequest body, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await WithDetailAsync(await _mediator.Send(new CancelCashFundingRequestCommand(id, body.Reason), ct), ct)
        );

    /// <summary>
    /// Tras un comando exitoso devuelve el detalle completo (nombres, resumen, acciones). Si el
    /// actor no puede verlo (p. ej. cajero con <c>fulfill</c> sin <c>view</c>), conserva la
    /// respuesta del comando: nunca se amplía el acceso por esta re-lectura.
    /// </summary>
    private async Task<Result<CashFundingRequestDto>> WithDetailAsync(
        Result<CashFundingRequestDto> result,
        CancellationToken ct
    )
    {
        if (!result.IsSuccess || result.Value is null)
            return result;
        var detail = await _mediator.Send(new GetCashFundingRequestByIdQuery(result.Value.Id), ct);
        return detail.IsSuccess ? Result<CashFundingRequestDto>.Success(detail.Value!, result.Code) : result;
    }
}

public sealed record CashFundingRequestReasonRequest(string Reason);
