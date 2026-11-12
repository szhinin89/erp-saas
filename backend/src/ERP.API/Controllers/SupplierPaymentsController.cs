using ERP.API.Contracts;
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

    /// <summary>Registra y confirma un pago a proveedor aplicándolo a cuotas de cuentas por pagar.</summary>
    /// <remarks>
    /// Operación de dinero en una sola transacción (sin borrador): descuenta el saldo de las cuotas indicadas,
    /// registra el egreso en caja o banco según cada línea de medio de pago y deja la obligación contable.
    /// Si el total supera lo aplicado, <c>confirmUnappliedAmount</c> debe ser <c>true</c> y el remanente queda como
    /// anticipo a favor del proveedor (<c>supplierCreditId</c>); sin aplicaciones solo se permite si la empresa tiene
    /// activa la política de pago sin cuenta por pagar (ver <c>GET policy</c>). Cada línea de medio usa exactamente una
    /// cuenta bancaria (con <c>transactionDate</c> obligatoria) o una caja (con sesión abierta controlada por el
    /// usuario y en la sucursal activa, y sin sobregiro).
    /// Idempotencia: <c>clientRequestId</c> es obligatorio; repetir la misma solicitud devuelve el pago ya
    /// registrado (también con 201) sin volver a aplicar ni mover dinero, y reutilizarlo con datos distintos responde 409.
    /// Un número de comprobante ya usado por el proveedor responde 409. La sucursal activa se registra como origen.
    /// </remarks>
    /// <param name="body">Proveedor, fecha, total, comprobante, líneas de medio de pago, aplicaciones a cuotas y matriz de distribución.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Pago registrado (o pago ya existente para el mismo <c>clientRequestId</c>).</response>
    /// <response code="404">Una cuenta bancaria, caja o cuota indicada no existe o no pertenece a la empresa.</response>
    /// <response code="409">Comprobante duplicado para el proveedor, o <c>clientRequestId</c> reutilizado con datos distintos.</response>
    /// <response code="422">Datos o reglas de pago inválidos (medio inactivo, montos, caja sin sesión abierta, saldo insuficiente, cuota ya pagada, falta de confirmación del remanente, etc.).</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
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
                    body.ApplicationLines ?? [],
                    body.Allocations ?? [],
                    body.ConfirmUnappliedAmount,
                    body.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — política de empresa que necesita el formulario (solo UX).</summary>
    /// <remarks>
    /// <c>allowWithoutPayable</c> indica si la empresa permite registrar un pago sin aplicarlo a cuotas (queda
    /// íntegro como anticipo). La regla definitiva se revalida siempre al registrar el pago.
    /// </remarks>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Política vigente de pagos a proveedor.</response>
    [HttpGet("policy")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPaymentPolicyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicy(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSupplierPaymentPolicyQuery(), ct), "OK");

    /// <summary>Obtiene el detalle de un pago a proveedor con sus líneas de medio, aplicaciones y distribución.</summary>
    /// <param name="id">Identificador del pago.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Pago encontrado.</response>
    /// <response code="404">El pago no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetSupplierPaymentByIdQuery(id), ct));

    /// <summary>Reversa un pago a proveedor confirmado (corrección documental, no devolución de dinero).</summary>
    /// <remarks>
    /// Solo un pago <c>Confirmed</c> puede reversarse (no hay doble reversa). En una sola transacción restituye el
    /// saldo de las cuotas aplicadas, genera el asiento contable inverso y anula el anticipo originado por el
    /// remanente, que debe seguir íntegro. Con líneas de caja exige <c>cashNotDeliveredConfirmed</c> y el ingreso
    /// compensatorio solo procede en la sesión de caja original, abierta, controlada por el usuario y de la sucursal
    /// activa; con líneas bancarias exige <c>bankReversalReason</c>. Si algo falla no queda ningún cambio parcial.
    /// </remarks>
    /// <param name="id">Identificador del pago a reversar.</param>
    /// <param name="body">Motivo de la reversa y confirmaciones de caja o razón bancaria.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Pago reversado.</response>
    /// <response code="404">El pago no existe en la empresa.</response>
    /// <response code="422">Reglas no cumplidas (motivo vacío, pago no confirmado, anticipo ya usado, sesión de caja cerrada o ajena, falta de confirmación, modificación concurrente, período contable cerrado, etc.).</response>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.Reverse}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reverse(
        Guid id,
        [FromBody] ReverseSupplierPaymentRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReverseSupplierPaymentCommand(
                    id,
                    body.Reason,
                    body.CashNotDeliveredConfirmed,
                    body.BankReversalReason
                ),
                ct
            )
        );

    /// <summary>Lista pagos a proveedor de la empresa con paginación.</summary>
    /// <param name="supplierId">Filtra por proveedor.</param>
    /// <param name="status">Filtra por estado del pago (sin distinguir mayúsculas); un valor desconocido se ignora y no filtra.</param>
    /// <param name="page">Número de página, desde 1.</param>
    /// <param name="pageSize">Tamaño de página (25 por defecto).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Página de pagos con el total de coincidencias.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SupplierPaymentsPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPaymentsListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetSupplierPaymentsListQuery(supplierId, status, page, pageSize),
                ct
            ),
            "OK"
        );
}
