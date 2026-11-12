using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — expone la liquidación de AR (<c>Payment</c>,
/// Fase 5.5.5.3). Controller delgado: solo recibe el command, lo envía vía MediatR y traduce el
/// <c>Result</c> — toda la lógica de negocio (balance, límites de saldo, transiciones de estado)
/// vive en <c>Payment</c>/<c>SalesReceivable</c> (<see cref="RegisterCollectionCommandHandler"/>),
/// reutilizada sin cambios.
/// PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — el endpoint AP (<c>POST /payments</c>,
/// <c>RegisterPaymentCommand</c> contra <c>AccountsPayable</c>) fue eliminado junto con su única
/// UI (<c>AccountsPayablePage</c>/<c>RegisterPaymentModal</c>, ya eliminadas): no debe quedar un
/// endpoint activo de registro de pago a proveedor sin pantalla ni diseño — cuando exista el
/// módulo PagoCabecera/PagoDetalle, se implementará ahí desde cero, no reviviendo este endpoint.
/// </summary>
[ApiController]
[Route("api/v1/finance")]
[Authorize]
[Produces("application/json")]
public sealed class FinancePaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinancePaymentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Registra un cobro (AR) aplicado contra una o más CxC del cliente indicado.</summary>
    /// <remarks>
    /// Idempotente por <c>clientRequestId</c> (obligatorio, estable en reintentos): el mismo id con los
    /// mismos datos devuelve el cobro ya registrado sin volver a aplicar la CxC, contabilizar ni encolar
    /// eventos; el mismo id con datos distintos responde 409. Cobro, aplicación a la CxC, asiento y
    /// outbox se confirman en una única transacción; los cobros concurrentes sobre la misma CxC se
    /// serializan y cada monto se valida contra el saldo vigente. Puede indicarse una cuenta bancaria
    /// <b>o</b> una caja destino (mutuamente excluyentes, ambas deben existir, estar activas y ser de la
    /// empresa); este endpoint no crea movimientos de caja ni usa sesión de caja. Cada línea debe
    /// pertenecer al cliente del cobro y, si indica cuota, no puede exceder su saldo.
    /// </remarks>
    /// <param name="command">Cliente, monto, fecha, destino opcional y líneas de aplicación por CxC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Cobro registrado (o cobro previo equivalente en un reintento).</response>
    /// <response code="404">Alguna cuenta por cobrar de las líneas no existe.</response>
    /// <response code="409">El <c>clientRequestId</c> ya se usó con datos distintos.</response>
    /// <response code="422">Datos inválidos, destino inexistente/inactivo, CxC de otro cliente, cuota inválida o monto que excede el saldo.</response>
    [HttpPost("collections")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = $"perm:{FinancePermissions.Create}")]
    public async Task<IActionResult> RegisterCollection(
        [FromBody] RegisterCollectionCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Lista los cobros aplicados a una cuenta por cobrar de la empresa activa.</summary>
    /// <param name="receivableId">Identificador de la cuenta por cobrar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cobros de la CxC (lista vacía si no tiene o no existe en la empresa).</response>
    [HttpGet("collections/receivable/{receivableId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PaymentDto>>), StatusCodes.Status200OK)]
    [Authorize(Policy = $"perm:{FinancePermissions.ReverseCollection}")]
    public async Task<IActionResult> GetCollections(Guid receivableId, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetReceivableCollectionsQuery(receivableId), ct));

    /// <summary>Cuerpo de <see cref="ReverseCollection"/>.</summary>
    /// <param name="Reason">Motivo de la anulación; obligatorio, máximo 500 caracteres.</param>
    public sealed record ReverseCollectionRequest(string Reason);

    /// <summary>Anula (reversa) un cobro aplicado y devuelve el saldo cobrado a cada CxC afectada.</summary>
    /// <remarks>
    /// Operación de corrección documental: no devuelve dinero ni compensa movimientos de caja o banco.
    /// Se rechaza si el cobro tiene movimientos operativos vinculados, si es un cobro histórico con
    /// destino caja/banco sin trazabilidad de origen, si la CxC está anulada o es de otra empresa, o si
    /// operaciones posteriores redujeron el saldo cobrado. Dos reversas del mismo cobro se serializan;
    /// la segunda se rechaza por la regla de dominio. Todo ocurre en una única transacción.
    /// </remarks>
    /// <param name="id">Identificador del cobro (pago).</param>
    /// <param name="request">Motivo de la anulación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cobro reversado.</response>
    /// <response code="404">El cobro o alguna de sus cuentas por cobrar no existe.</response>
    /// <response code="422">El pago no es un cobro, ya está reversado o no es reversable, o falta el motivo.</response>
    [HttpPost("collections/{id:guid}/reverse")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = $"perm:{FinancePermissions.ReverseCollection}")]
    public async Task<IActionResult> ReverseCollection(Guid id, [FromBody] ReverseCollectionRequest request,
        CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ReverseCollectionCommand(id, request.Reason), ct));
}
