using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Sesiones (turnos) de caja: apertura, cierre con arqueo, movimientos manuales de efectivo,
/// depósitos bancarios y consultas. Una sesión pertenece a la sucursal activa y la opera solo el
/// usuario que la abrió.
/// </summary>
[AppFeature("Caja", $"perm:{CajaPermissions.View}", "💵", "/treasury/cash", null, 70)]
[ApiController]
[Route("api/v1/cash-sessions")]
[Authorize]
[Produces("application/json")]
public sealed class CashSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashSessionController(IMediator mediator) => _mediator = mediator;

    /// <summary>Abre una sesión de caja para el usuario autenticado en la sucursal activa.</summary>
    /// <remarks>
    /// El cliente solo envía la caja, el monto de apertura y notas; empresa, sucursal y punto de
    /// emisión se resuelven en el servidor. Un usuario solo puede tener una sesión abierta y cada caja
    /// solo una sesión abierta. La caja debe estar activa, pertenecer a la sucursal activa y tener un
    /// punto de emisión habilitado para vender (ver <c>opening-readiness</c>); todo rechazo ocurre
    /// antes de crear la sesión. Ante dos aperturas concurrentes decide un índice único de base de
    /// datos y la perdedora recibe 409.
    /// </remarks>
    /// <param name="command">Caja, monto de apertura (no negativo) y notas opcionales.</param>
    /// <response code="201">Sesión abierta.</response>
    /// <response code="404">La caja no existe.</response>
    /// <response code="409">Apertura concurrente: ya existe una sesión abierta para la caja o el usuario.</response>
    /// <response code="422">El usuario ya tiene una sesión abierta; la caja está deshabilitada, es de otra sucursal, no tiene punto de emisión o no cumple los requisitos para abrir; o monto de apertura inválido.</response>
    [HttpPost("open")]
    [Authorize(Policy = $"perm:{CajaPermissions.Open}")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Open(
        [FromBody] OpenCashSessionCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Cierra una sesión de caja registrando el arqueo por denominaciones.</summary>
    /// <remarks>
    /// Solo cierra la sesión quien la abrió y mientras esté abierta. En una única transacción se
    /// cancelan las solicitudes de efectivo pendientes de la sesión (motivo «Caja cerrada») y se cierra
    /// la sesión. Solo se guardan las denominaciones con cantidad mayor que cero. Si el arqueo difiere
    /// del efectivo esperado, las preferencias de la empresa pueden rechazar el cierre (diferencia no
    /// permitida, mayor que el máximo o sin motivo en las notas); un rechazo no deja ningún cambio.
    /// </remarks>
    /// <param name="id">Id de la sesión de caja.</param>
    /// <param name="request">Arqueo (al menos una denominación) y notas de cierre opcionales.</param>
    /// <response code="200">Sesión cerrada.</response>
    /// <response code="404">La sesión no existe o no es de la sucursal activa.</response>
    /// <response code="422">La sesión no está abierta, la opera otro usuario, el arqueo es inválido o la diferencia no cumple las preferencias de la empresa.</response>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = $"perm:{CajaPermissions.Close}")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Close(
        Guid id,
        [FromBody] CloseCashSessionRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CloseCashSessionCommand(id, request.ClosingCounts, request.CloseNotes),
                ct
            )
        );

    /// <summary>Registra un movimiento manual de efectivo (ingreso manual, egreso manual o retiro).</summary>
    /// <remarks>
    /// <b>Idempotente</b> por <c>clientRequestId</c> (obligatorio): repetir la misma solicitud con los
    /// mismos datos devuelve el movimiento ya registrado sin duplicarlo; el mismo identificador con
    /// datos distintos responde 409. El registro se hace bajo bloqueo de la sesión, en una transacción.
    /// Requisitos: la empresa debe permitir movimientos manuales; la sesión debe estar abierta y operada
    /// por el usuario; el motivo debe existir, estar activo y corresponder al tipo de movimiento; los
    /// egresos y retiros no pueden superar el efectivo disponible de la sesión.
    /// </remarks>
    /// <param name="id">Id de la sesión de caja.</param>
    /// <param name="request">Tipo (<c>ManualIncome</c>, <c>ManualExpense</c> o <c>Withdrawal</c>), motivo, monto mayor que cero, descripción y clave de idempotencia.</param>
    /// <response code="201">Movimiento registrado (o el ya registrado con el mismo <c>clientRequestId</c>).</response>
    /// <response code="404">La sesión no existe o no es de la sucursal activa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Datos inválidos, movimientos manuales no permitidos, sesión no abierta u operada por otro usuario, motivo inválido o saldo de efectivo insuficiente.</response>
    [HttpPost("{id:guid}/movements")]
    [Authorize(Policy = $"perm:{CajaPermissions.Record}")]
    [ProducesResponseType(typeof(ApiResponse<CashMovementDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordMovement(
        Guid id,
        [FromBody] RecordCashMovementRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new RecordCashMovementCommand(
                    id,
                    request.MovementType,
                    request.ReasonId,
                    request.Amount,
                    request.Description,
                    request.ReferenceType,
                    request.ReferenceId,
                    request.ReferenceNumber,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>
    /// ACCOUNTING-12C — depósito del efectivo de la caja en una cuenta bancaria de la empresa
    /// (movimiento BankDeposit; asiento Debe banco / Haber caja). Mismo permiso que registrar
    /// movimientos de caja: es una salida de efectivo del turno que opera el cajero.
    /// </summary>
    /// <remarks>
    /// <b>Idempotente</b> por <c>clientRequestId</c> (obligatorio), con la misma semántica que
    /// <c>movements</c>: mismo identificador y mismos datos devuelven el depósito ya registrado; con
    /// datos distintos, 409. La sesión debe estar abierta y operada por el usuario, la cuenta bancaria
    /// debe ser de la empresa y estar activa, y el monto no puede superar el efectivo disponible.
    /// </remarks>
    /// <param name="id">Id de la sesión de caja.</param>
    /// <param name="request">Cuenta bancaria destino, monto mayor que cero, descripción, referencia opcional y clave de idempotencia.</param>
    /// <response code="201">Depósito registrado (o el ya registrado con el mismo <c>clientRequestId</c>).</response>
    /// <response code="404">La sesión no existe o no es de la sucursal activa.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Datos inválidos, sesión no abierta u operada por otro usuario, cuenta bancaria inexistente, ajena o inactiva, o saldo de efectivo insuficiente.</response>
    [HttpPost("{id:guid}/bank-deposits")]
    [Authorize(Policy = $"perm:{CajaPermissions.Record}")]
    [ProducesResponseType(typeof(ApiResponse<CashMovementDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordBankDeposit(
        Guid id,
        [FromBody] RecordCashBankDepositRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new RecordCashBankDepositCommand(
                    id,
                    request.CompanyBankAccountId,
                    request.Amount,
                    request.Description,
                    request.ReferenceNumber,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Obtiene una sesión de caja de la sucursal activa, con sus saldos y movimientos.</summary>
    /// <param name="id">Id de la sesión de caja.</param>
    /// <response code="200">Sesión encontrada.</response>
    /// <response code="404">La sesión no existe o no es de la sucursal activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashSessionByIdQuery(id), ct));

    /// <summary>Lista paginada de sesiones de caja de la sucursal activa.</summary>
    /// <param name="status">Estado: <c>Open</c> o <c>Closed</c> (sin distinguir mayúsculas). Un valor no reconocido no filtra.</param>
    /// <param name="pageNumber">Número de página (por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (por defecto 25).</param>
    /// <response code="200">Página de sesiones.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCashSessionListQuery(status, pageNumber, pageSize), ct),
            "OK"
        );

    /// <summary>Sesión de caja abierta del usuario autenticado.</summary>
    /// <remarks>Si el usuario no tiene una sesión abierta responde 200 con <c>data</c> nulo.</remarks>
    /// <response code="200">Sesión abierta del usuario, o <c>data</c> nulo si no tiene.</response>
    [HttpGet("my")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetMyCashSessionQuery(), ct), "OK");

    /// <summary>Resumen informativo de ventas y cobros del turno, por forma de pago.</summary>
    /// <remarks>Es independiente del efectivo físico de la sesión: no modifica ni sustituye sus saldos.</remarks>
    /// <param name="id">Id de la sesión de caja.</param>
    /// <response code="200">Resumen de facturas, total facturado, cobrado y a crédito.</response>
    /// <response code="404">La sesión no existe o no es de la sucursal activa.</response>
    [HttpGet("{id:guid}/collection-summary")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<CashSessionCollectionSummaryDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCollectionSummary(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashSessionCollectionSummaryQuery(id), ct));
}

public sealed record CloseCashSessionRequest(
    List<CashClosingCountInput> ClosingCounts,
    string? CloseNotes = null
);

public sealed record RecordCashMovementRequest(
    string MovementType,
    Guid ReasonId,
    decimal Amount,
    string Description,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? ReferenceNumber = null,
    Guid ClientRequestId = default
);

public sealed record RecordCashBankDepositRequest(
    Guid CompanyBankAccountId,
    decimal Amount,
    string Description,
    string? ReferenceNumber = null,
    Guid ClientRequestId = default
);
