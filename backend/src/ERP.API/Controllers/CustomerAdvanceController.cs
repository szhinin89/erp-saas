using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.UseCases.CustomerAdvances;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// CUSTOMER-ADVANCES-V1 (ADR-052) — anticipos de clientes: registro, consultas, aplicación a cuentas
/// por cobrar y su reversión, devolución de dinero y su reversión, reversa del ingreso y anulación
/// administrativa. Controller delgado: HTTP → MediatR → <c>Result</c>. Sin <c>[AppFeature]</c>: no
/// tiene navegación propia.
/// </summary>
[ApiController]
[Route("api/v1/finance/customer-advances")]
[Authorize]
[Produces("application/json")]
public sealed class CustomerAdvanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomerAdvanceController(IMediator mediator) => _mediator = mediator;

    /// <summary>Registra un anticipo de cliente en efectivo o por transferencia bancaria.</summary>
    /// <remarks>
    /// Confirmación directa y atómica: anticipo, movimiento inicial, auditoría, obligación contable y,
    /// en efectivo, el ingreso en el turno abierto de la caja. Exactamente un destino: caja (efectivo,
    /// solo el titular del turno) o cuenta bancaria (transferencia). Idempotente por
    /// <c>clientRequestId</c> (obligatorio, estable en reintentos): con los mismos datos devuelve el
    /// anticipo ya registrado; con datos distintos responde 409. No crea factura, cuenta por cobrar ni
    /// cobro. Tenant, empresa, sucursal activa, moneda y usuario salen del contexto.
    /// </remarks>
    /// <param name="command">Cliente, monto, fecha, medio de pago, destino, referencia y motivo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Anticipo registrado (o el equivalente previo en un reintento).</response>
    /// <response code="404">El cliente, medio de pago o destino no existe en la empresa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Datos inválidos, fecha posterior al día de negocio, medio o destino no permitido o inactivo, sin turno propio abierto o período contable cerrado.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesCreate}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCustomerAdvanceCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Cuerpo de <see cref="Apply"/>.</summary>
    /// <param name="EffectiveDate">Fecha efectiva (y contable) de la aplicación: no anterior al anticipo ni al origen de las cuentas, ni posterior al día de negocio.</param>
    /// <param name="Lines">CxC, cuota opcional y monto de cada aplicación.</param>
    /// <param name="ClientRequestId">Intención del cliente, estable en reintentos; obligatoria.</param>
    public sealed record ApplyRequest(
        DateOnly EffectiveDate,
        IReadOnlyList<CustomerAdvanceApplicationInput> Lines,
        Guid ClientRequestId
    );

    /// <summary>Cuerpo de <see cref="ReverseApplication"/>.</summary>
    /// <param name="Reason">Motivo; obligatorio, máximo 500 caracteres.</param>
    /// <param name="Reference">Referencia que respalda la reversa; obligatoria, máximo 100.</param>
    /// <param name="ClientRequestId">Intención del cliente, estable en reintentos; obligatoria.</param>
    public sealed record ReverseApplicationRequest(
        string Reason,
        string Reference,
        Guid ClientRequestId
    );

    /// <summary>Aplica el anticipo a una o varias cuentas por cobrar del mismo cliente.</summary>
    /// <remarks>
    /// Atómica: una línea inválida rechaza toda la operación. No mueve dinero ni crea cobros; reduce
    /// cada cuenta por cobrar (cuota indicada o por orden de cuota) y genera un movimiento y una
    /// obligación contable por línea. Admite cuentas de otras sucursales de la misma empresa.
    /// La fecha efectiva no puede ser anterior a la fecha del anticipo ni a la fecha de origen contable
    /// de ninguna cuenta (emisión de la factura o apertura de saldos), ni posterior al día de negocio
    /// de la empresa.
    /// Idempotente por <c>clientRequestId</c>: con los mismos datos devuelve el resultado original;
    /// con datos distintos responde 409.
    /// </remarks>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="request">Fecha, líneas e identificador de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo con la aplicación registrada.</response>
    /// <response code="404">El anticipo o alguna cuenta por cobrar no existe en la empresa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Datos inválidos, saldo insuficiente, cuenta de otro cliente o anulada, cuota inválida, cuotas inconsistentes, fecha incompatible o período contable cerrado.</response>
    [HttpPost("{id:guid}/applications")]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesApply}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Apply(
        Guid id,
        [FromBody] ApplyRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ApplyCustomerAdvanceCommand(
                    id,
                    request.EffectiveDate,
                    request.Lines,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Revierte completa una aplicación del anticipo.</summary>
    /// <remarks>
    /// Restaura el saldo del anticipo y de cada cuenta por cobrar con los importes originales, un
    /// movimiento compensatorio por línea y el reverso contable oficial. No devuelve dinero. La fecha
    /// es el día de negocio de la reversa. Se rechaza completa si alguna cuenta está anulada, no
    /// conserva cobrado suficiente o sus cuotas no cuadran. Idempotente por <c>clientRequestId</c>.
    /// </remarks>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="operationId">Operación de aplicación a revertir (<c>operationId</c> de sus movimientos).</param>
    /// <param name="request">Motivo, referencia e identificador de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo con la reversa registrada.</response>
    /// <response code="404">El anticipo, la operación o alguna cuenta por cobrar no existe.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Falta motivo o referencia, aplicación ya revertida, cuenta incompatible o período contable cerrado.</response>
    [HttpPost("{id:guid}/applications/{operationId:guid}/reverse")]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesReverse}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReverseApplication(
        Guid id,
        Guid operationId,
        [FromBody] ReverseApplicationRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReverseCustomerAdvanceApplicationCommand(
                    id,
                    operationId,
                    request.Reason,
                    request.Reference,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Cuerpo de <see cref="Refund"/>.</summary>
    /// <param name="Amount">Monto a devolver; mayor a cero y no superior al saldo disponible.</param>
    /// <param name="EffectiveDate">Fecha efectiva: entre la fecha del anticipo y el día de negocio.</param>
    /// <param name="PaymentMethodId">Medio de pago: efectivo o transferencia bancaria.</param>
    /// <param name="CompanyBankAccountId">Cuenta bancaria de origen; excluyente con la caja.</param>
    /// <param name="CashRegisterId">Caja de la que sale el efectivo; excluyente con la cuenta bancaria.</param>
    /// <param name="Reference">Referencia de ejecución; obligatoria, máximo 100.</param>
    /// <param name="Reason">Motivo; obligatorio, máximo 500.</param>
    /// <param name="ClientRequestId">Intención del cliente, estable en reintentos; obligatoria.</param>
    public sealed record RefundRequest(
        decimal Amount,
        DateOnly EffectiveDate,
        Guid PaymentMethodId,
        Guid? CompanyBankAccountId,
        Guid? CashRegisterId,
        string Reference,
        string Reason,
        Guid ClientRequestId
    );

    /// <summary>Cuerpo de una reversa respaldada por evidencia de que el dinero se movió de vuelta.</summary>
    /// <param name="Reason">Motivo; obligatorio, máximo 500.</param>
    /// <param name="Reference">Referencia de la evidencia; obligatoria, máximo 100.</param>
    /// <param name="EvidenceDate">Fecha que consta en la evidencia, entre la operación original y el día de negocio; no es la fecha contable.</param>
    /// <param name="ClientRequestId">Intención del cliente, estable en reintentos; obligatoria.</param>
    public sealed record EvidencedReversalRequest(
        string Reason,
        string Reference,
        DateOnly EvidenceDate,
        Guid ClientRequestId
    );

    /// <summary>Cuerpo de <see cref="Cancel"/>.</summary>
    /// <param name="Reason">Motivo; obligatorio, máximo 500.</param>
    /// <param name="Reference">Referencia de la evidencia; obligatoria, máximo 100.</param>
    /// <param name="MoneyNotReceived">Declaración expresa de que el dinero no fue recibido; obligatoria.</param>
    /// <param name="ClientRequestId">Intención del cliente, estable en reintentos; obligatoria.</param>
    public sealed record CancelRequest(
        string Reason,
        string Reference,
        bool MoneyNotReceived,
        Guid ClientRequestId
    );

    /// <summary>Devuelve al cliente, total o parcialmente, el saldo disponible del anticipo.</summary>
    /// <remarks>
    /// Efectivo desde el turno abierto de una caja propia y con fondos, o transferencia contra una cuenta
    /// bancaria; el destino puede ser distinto del ingreso. No devuelve lo aplicado a cuentas por cobrar.
    /// La transferencia solo se registra: no se ejecuta en el banco.
    /// </remarks>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="request">Datos de la operación e identificador de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo con la devolución registrada.</response>
    /// <response code="404">El anticipo, el medio o el destino no existen en la empresa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Datos inválidos, saldo insuficiente, anticipo cancelado, destino no admitido o inactivo, turno o fondos insuficientes, fecha incompatible o período contable cerrado.</response>
    [HttpPost("{id:guid}/refunds")]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesRefund}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new RegisterCustomerAdvanceRefundCommand(
                    id,
                    request.Amount,
                    request.EffectiveDate,
                    request.PaymentMethodId,
                    request.CompanyBankAccountId,
                    request.CashRegisterId,
                    request.Reference,
                    request.Reason,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Revierte una devolución que no llegó a ejecutarse y cuyo dinero sigue en la empresa.</summary>
    /// <remarks>
    /// Restaura exactamente el importe de la devolución en su mismo destino; en efectivo ingresa en el
    /// turno abierto de la caja original. La fecha contable es el día de negocio de la reversa. Una
    /// devolución entregada al cliente no se revierte.
    /// </remarks>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="transactionId">Transacción de devolución (<c>refundTransactions[].id</c>).</param>
    /// <param name="request">Datos de la operación e identificador de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo con la devolución revertida.</response>
    /// <response code="404">El anticipo o la devolución no existen en la empresa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Falta motivo, referencia o fecha de evidencia, devolución ya revertida, anticipo cancelado, turno no disponible o período contable cerrado.</response>
    [HttpPost("{id:guid}/refunds/{transactionId:guid}/reverse")]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesRefund}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReverseRefund(
        Guid id,
        Guid transactionId,
        [FromBody] EvidencedReversalRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReverseCustomerAdvanceRefundCommand(
                    id,
                    transactionId,
                    request.Reason,
                    request.Reference,
                    request.EvidenceDate,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Revierte completo el ingreso del anticipo porque el dinero realmente se deshizo.</summary>
    /// <remarks>
    /// Exige el anticipo íntegro y evidencia. En efectivo el dinero sale del turno abierto de la caja
    /// original, con fondos; en transferencia no hay movimiento de caja. El anticipo queda cancelado y su
    /// asiento se espeja.
    /// </remarks>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="request">Datos de la operación e identificador de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo cancelado por la reversa del ingreso.</response>
    /// <response code="404">El anticipo no existe en la empresa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Falta motivo, referencia o fecha de evidencia, anticipo no íntegro o cancelado, turno o fondos insuficientes o período contable cerrado.</response>
    [HttpPost("{id:guid}/reverse-income")]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesRefund}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReverseIncome(
        Guid id,
        [FromBody] EvidencedReversalRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReverseCustomerAdvanceIncomeCommand(
                    id,
                    request.Reason,
                    request.Reference,
                    request.EvidenceDate,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Anula administrativamente un anticipo cuyo dinero nunca fue recibido.</summary>
    /// <remarks>
    /// Solo un anticipo íntegro, por transferencia y sin asiento publicado; con la declaración expresa.
    /// No mueve caja ni banco ni genera asientos: cancela la obligación contable pendiente.
    /// </remarks>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="request">Datos de la operación e identificador de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo anulado.</response>
    /// <response code="404">El anticipo no existe en la empresa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Falta motivo, referencia o declaración, anticipo no íntegro, en efectivo, cancelado o ya contabilizado.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{FinancePermissions.CustomerAdvancesReverse}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CancelCustomerAdvanceCommand(
                    id,
                    request.Reason,
                    request.Reference,
                    request.MoneyNotReceived,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Aplicaciones de anticipos recibidas por una cuenta por cobrar, vigentes y revertidas.</summary>
    /// <param name="receivableId">Identificador de la cuenta por cobrar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Aplicaciones en orden de registro (lista vacía si no tiene).</response>
    /// <response code="404">La cuenta por cobrar no existe en la empresa activa.</response>
    [HttpGet("by-receivable/{receivableId:guid}")]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CustomerAdvanceReceivableApplicationDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByReceivable(Guid receivableId, CancellationToken ct) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetReceivableAdvanceApplicationsQuery(receivableId), ct)
        );

    /// <summary>Lista los anticipos de la empresa activa, del más reciente al más antiguo.</summary>
    /// <remarks>La sucursal es un filtro opcional; sin él se listan los de todas las sucursales.</remarks>
    /// <param name="page">Página (base 1; por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (1–100; por defecto 25).</param>
    /// <param name="customerId">Filtra por cliente.</param>
    /// <param name="status">Filtra por estado (<c>Confirmed</c> o <c>Cancelled</c>).</param>
    /// <param name="branchId">Filtra por sucursal de origen.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Página de anticipos con el total.</response>
    /// <response code="422">Paginación o filtro fuera de rango.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<CustomerAdvanceListResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? customerId = null,
        [FromQuery] CustomerAdvanceStatus? status = null,
        [FromQuery] Guid? branchId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetCustomerAdvanceListQuery(page, pageSize, customerId, status, branchId),
                ct
            ),
            "OK"
        );

    /// <summary>Obtiene un anticipo con su historial de movimientos.</summary>
    /// <param name="id">Identificador del anticipo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Anticipo encontrado.</response>
    /// <response code="404">El anticipo no existe en la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCustomerAdvanceByIdQuery(id), ct));

    /// <summary>Saldo disponible de un cliente en la empresa activa, sumando todas las sucursales.</summary>
    /// <param name="customerId">Cliente a consultar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Saldo disponible (cero si no tiene anticipos vigentes).</response>
    /// <response code="404">El cliente no existe.</response>
    /// <response code="422">Falta el cliente.</response>
    [HttpGet("balance")]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAdvanceBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetBalance(
        [FromQuery] Guid customerId,
        CancellationToken ct
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetCustomerAdvanceBalanceQuery(customerId), ct)
        );
}
