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
/// CUSTOMER-ADVANCES-V1 CA-F3 (ADR-052) — registro y consulta de anticipos de clientes. Controller
/// delgado: HTTP → MediatR → <c>Result</c>. Sin <c>[AppFeature]</c>: la navegación del módulo no
/// pertenece a esta fase.
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
    /// <response code="422">Datos inválidos, medio o destino no permitido o inactivo, sin turno propio abierto o período contable cerrado.</response>
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
