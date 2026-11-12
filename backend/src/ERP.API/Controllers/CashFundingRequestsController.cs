using ERP.API.Contracts;
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

    /// <summary>Crea una solicitud de efectivo a un cajero para pagar a un proveedor.</summary>
    /// <remarks>
    /// El cuerpo es el mismo pago a proveedor que se registraría directamente, más <c>clientRequestId</c>
    /// (obligatorio). <b>No produce ningún efecto financiero</b>: guarda la intención de pago y queda en
    /// estado <c>Pending</c>; el pago solo se ejecuta cuando el cajero la entrega (<c>fulfill</c>).
    /// <b>Idempotente</b>: repetir el mismo <c>clientRequestId</c> con los mismos datos devuelve la
    /// solicitud ya creada; con datos distintos responde 409. Requisitos: exactamente una línea de efectivo
    /// (las demás deben ser bancarias); la caja de esa línea debe tener una sesión abierta en la sucursal
    /// activa y no estar operada por el propio solicitante (en ese caso debe registrar el pago
    /// directamente); el pago debe cumplir las mismas validaciones que el pago directo (medios, destinos,
    /// cuotas por pagar, proveedor, comprobante y efectivo disponible, sin reservarlo).
    /// </remarks>
    /// <param name="body">Pago a proveedor (proveedor, fecha, total, líneas de medio de pago, aplicaciones y asignaciones) y clave de idempotencia.</param>
    /// <response code="201">Solicitud creada (o la ya existente con el mismo <c>clientRequestId</c>), con su detalle.</response>
    /// <response code="404">Una cuenta bancaria, caja o cuota referida por el pago no existe.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos, o ya existe un pago con ese comprobante para el proveedor.</response>
    /// <response code="422">Datos inválidos: forma de las líneas de efectivo, caja sin sesión abierta, caja operada por el solicitante o de otra sucursal, o el pago no cumple las validaciones.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<CashFundingRequestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCashFundingRequestRequest body,
        CancellationToken ct
    )
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
        var result = await _mediator.Send(
            new CreateCashFundingRequestCommand(payment, body.ClientRequestId),
            ct
        );
        return this.ToCreatedOrBadRequest(await WithDetailAsync(result, ct));
    }

    /// <summary>Bandeja de caja: empresa operativa + sucursal activa, siempre.</summary>
    /// <param name="status">Estado: <c>Pending</c>, <c>Fulfilled</c>, <c>Rejected</c> o <c>Cancelled</c>; omitido devuelve todos.</param>
    /// <param name="cashRegisterId">Filtra por caja (opcional).</param>
    /// <param name="requestedByUserId">Filtra por usuario solicitante (opcional).</param>
    /// <param name="page">Número de página (por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (por defecto 25).</param>
    /// <response code="200">Página de solicitudes de la sucursal activa.</response>
    /// <response code="422">Estado de solicitud no válido o no hay sucursal activa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.FundingRequestsView}")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<CashFundingRequestListItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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
                new GetCashFundingRequestListQuery(
                    status,
                    cashRegisterId,
                    requestedByUserId,
                    page,
                    pageSize
                ),
                ct
            )
        );

    /// <summary>"Mis solicitudes": el solicitante es siempre el usuario autenticado.</summary>
    /// <param name="status">Estado: <c>Pending</c>, <c>Fulfilled</c>, <c>Rejected</c> o <c>Cancelled</c>; omitido devuelve todos.</param>
    /// <param name="page">Número de página (por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (por defecto 25).</param>
    /// <response code="200">Página de solicitudes del usuario autenticado.</response>
    /// <response code="422">Estado de solicitud no válido.</response>
    [HttpGet("mine")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<CashFundingRequestListItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetMine(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetMyCashFundingRequestsQuery(status, page, pageSize), ct)
        );

    /// <summary>
    /// Solicitante O <c>caja.funding-requests.view</c>: la regla OR se resuelve en Application con el
    /// autorizador oficial (no hay políticas OR). Sin acceso → 404.
    /// </summary>
    /// <remarks>Devuelve además las acciones disponibles para el usuario sobre la solicitud (entregar/rechazar/cancelar).</remarks>
    /// <param name="id">Id de la solicitud de efectivo.</param>
    /// <response code="200">Detalle de la solicitud.</response>
    /// <response code="404">La solicitud no existe o el usuario no puede verla (no se revela su existencia).</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CashFundingRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashFundingRequestByIdQuery(id), ct));

    /// <summary>El cajero entrega el efectivo: ejecuta el pago a proveedor de la solicitud.</summary>
    /// <remarks>
    /// <b>Mueve dinero.</b> En una sola transacción, con la sesión de caja y la solicitud bloqueadas,
    /// se revalida todo el pago y se crea el pago al proveedor con su movimiento de caja y su
    /// contabilización (y el crédito del proveedor si corresponde); si algo falla se revierte todo y la
    /// solicitud sigue <c>Pending</c>. Solo puede entregarla quien controla la sesión de caja abierta de
    /// la solicitud en la sucursal activa (el permiso no sustituye esa condición). El pago se ejecuta
    /// a nombre del solicitante, con el cajero como ejecutor. <b>Idempotente</b>: reintentar sobre una
    /// solicitud ya <c>Fulfilled</c> devuelve la misma solicitud sin volver a pagar; una solicitud
    /// rechazada o cancelada no puede entregarse.
    /// </remarks>
    /// <param name="id">Id de la solicitud de efectivo.</param>
    /// <response code="200">Solicitud entregada (<c>Fulfilled</c>), con su detalle.</response>
    /// <response code="404">La solicitud o su sesión de caja no existe.</response>
    /// <response code="409">El pago no puede registrarse porque ya existe un pago con ese comprobante para el proveedor.</response>
    /// <response code="422">La solicitud ya fue resuelta, la sesión ya no está abierta o es de otra sucursal, el usuario no opera la caja, la intención guardada no es íntegra o el pago ya no cumple las validaciones.</response>
    [HttpPost("{id:guid}/fulfill")]
    [Authorize(Policy = $"perm:{CajaPermissions.FundingRequestsFulfill}")]
    [ProducesResponseType(typeof(ApiResponse<CashFundingRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Fulfill(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await WithDetailAsync(
                await _mediator.Send(new FulfillCashFundingRequestCommand(id), ct),
                ct
            )
        );

    /// <summary>El cajero rechaza una solicitud pendiente indicando el motivo.</summary>
    /// <remarks>
    /// Sin efectos financieros. Solo aplica a solicitudes <c>Pending</c> y solo puede rechazarla quien
    /// controla la sesión de caja abierta de la solicitud en la sucursal activa.
    /// </remarks>
    /// <param name="id">Id de la solicitud de efectivo.</param>
    /// <param name="body">Motivo del rechazo (obligatorio).</param>
    /// <response code="200">Solicitud rechazada (<c>Rejected</c>), con su detalle.</response>
    /// <response code="404">La solicitud o su sesión de caja no existe.</response>
    /// <response code="422">Motivo inválido, solicitud ya resuelta, sesión no abierta o de otra sucursal, o el usuario no opera la caja.</response>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = $"perm:{CajaPermissions.FundingRequestsFulfill}")]
    [ProducesResponseType(typeof(ApiResponse<CashFundingRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] CashFundingRequestReasonRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await WithDetailAsync(
                await _mediator.Send(new RejectCashFundingRequestCommand(id, body.Reason), ct),
                ct
            )
        );

    /// <summary>El solicitante cancela su solicitud pendiente indicando el motivo.</summary>
    /// <remarks>
    /// Sin efectos financieros. Solo quien creó la solicitud puede cancelarla y solo mientras esté
    /// <c>Pending</c>. Las solicitudes pendientes también se cancelan automáticamente al cerrar la
    /// sesión de caja (motivo «Caja cerrada»).
    /// </remarks>
    /// <param name="id">Id de la solicitud de efectivo.</param>
    /// <param name="body">Motivo de la cancelación (obligatorio).</param>
    /// <response code="200">Solicitud cancelada (<c>Cancelled</c>), con su detalle.</response>
    /// <response code="403">El usuario no es quien solicitó el efectivo (envelope <c>FORBIDDEN</c>).</response>
    /// <response code="404">La solicitud no existe.</response>
    /// <response code="422">Motivo inválido o solicitud ya resuelta.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<CashFundingRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CashFundingRequestReasonRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await WithDetailAsync(
                await _mediator.Send(new CancelCashFundingRequestCommand(id, body.Reason), ct),
                ct
            )
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
        return detail.IsSuccess
            ? Result<CashFundingRequestDto>.Success(detail.Value!, result.Code)
            : result;
    }
}

public sealed record CashFundingRequestReasonRequest(string Reason);
