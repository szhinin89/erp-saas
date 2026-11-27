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
/// por cobrar y reversión de aplicaciones. Controller delgado: HTTP → MediatR → <c>Result</c>. Sin
/// <c>[AppFeature]</c>: no tiene navegación propia.
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
