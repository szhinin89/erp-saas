using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-02 Fase 11 — expone los casos de uso ya implementados de <c>SupplierCredit</c> (Fases 2, 7,
/// 8) — sin permisos nuevos: reutiliza <see cref="FinancePermissions"/> (diseño §20.2: aplicar
/// crédito a otra CxP, reembolsar y revertir son operaciones que mueven dinero/afectan CxP,
/// separadas deliberadamente de "autorizar la devolución física" que vive en
/// <see cref="PurchaseReturnController"/> con <see cref="PurchasePermissions"/> — mismo riesgo
/// operativo que <c>RegisterPayment</c>, ya en Finance). Controller delgado: solo mapea
/// HTTP → MediatR → <c>Result</c>; toda la lógica de negocio (locks A→B, idempotencia,
/// revalidación de saldo) vive en los handlers ya construidos, reutilizados sin cambios.
/// </summary>
[ApiController]
[Route("api/v1/finance/supplier-credits")]
[Authorize]
[Produces("application/json")]
public sealed class SupplierCreditController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupplierCreditController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // CONSULTAS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Obtiene un crédito de proveedor por Id, con su historial de movimientos.</summary>
    /// <response code="200">Crédito encontrado.</response>
    /// <response code="404">El crédito no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierCreditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetSupplierCreditByIdQuery(id), ct));

    /// <summary>Lista créditos de proveedor paginados.</summary>
    /// <response code="200">Listado paginado.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierCreditListResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSupplierCreditListQuery(page, pageSize), ct),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // APLICACIÓN
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aplica el crédito contra una <c>PurchasePayable</c> destino (Lock A del destino → Lock B
    /// del crédito, en ese orden fijo). Idempotente por <c>ClientRequestId</c> (§16.2).
    /// </summary>
    /// <response code="200">Crédito aplicado.</response>
    /// <response code="404">El crédito o la cuenta por pagar destino no existen.</response>
    /// <response code="422">
    /// CxP cancelada, proveedor/moneda distintos, monto insuficiente/excede el saldo, o conflicto
    /// de idempotencia.
    /// </response>
    [HttpPost("{id:guid}/apply")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierCreditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Apply(
        Guid id,
        [FromBody] ApplySupplierCreditRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ApplySupplierCreditCommand(
                    id,
                    request.TargetPurchasePayableId,
                    request.Amount,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Revierte una aplicación previa — nunca edita el movimiento original, crea uno nuevo.</summary>
    /// <response code="200">Aplicación revertida.</response>
    /// <response code="404">El crédito no existe.</response>
    /// <response code="422">
    /// Movimiento inexistente/no es una aplicación/ya revertido, CxP destino cancelada (SC-014),
    /// o conflicto de idempotencia.
    /// </response>
    [HttpPost("{id:guid}/apply/{movementId:guid}/reverse")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierCreditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReverseApplication(
        Guid id,
        Guid movementId,
        [FromBody] ReverseSupplierCreditApplicationRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReverseSupplierCreditApplicationCommand(
                    id,
                    movementId,
                    request.TargetPurchasePayableId,
                    request.ClientRequestId
                ),
                ct
            )
        );

    // ══════════════════════════════════════════════════════════════════════
    // REEMBOLSO
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registra un reembolso del crédito contra un destino financiero (banco/caja) — resuelve
    /// destino + cuenta contable + crea el movimiento de Caja si aplica, todo en una única
    /// transacción. Idempotente por <c>ClientRequestId</c> (§16.2ter).
    /// </summary>
    /// <response code="200">Reembolso registrado.</response>
    /// <response code="404">El crédito o el destino financiero no existen.</response>
    /// <response code="422">
    /// Destino inactivo, moneda distinta, método de pago inválido, monto insuficiente, o
    /// conflicto de idempotencia.
    /// </response>
    [HttpPost("{id:guid}/refund")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierCreditRefundTransactionDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RegisterSupplierCreditRefundRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new RegisterSupplierCreditRefundCommand(
                    id,
                    request.FinancialDestinationId,
                    request.PaymentMethodCode,
                    request.Amount,
                    request.EffectiveDate,
                    request.ExternalReference,
                    request.ClientRequestId
                ),
                ct
            )
        );

    /// <summary>Revierte un reembolso previo — nunca edita el movimiento original, crea uno nuevo.</summary>
    /// <response code="200">Reembolso revertido.</response>
    /// <response code="404">El crédito o la transacción de reembolso original no existen.</response>
    /// <response code="422">Ya revertido, sesión de caja no disponible, o conflicto de idempotencia.</response>
    [HttpPost("{id:guid}/refund/{movementId:guid}/reverse")]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierCreditRefundTransactionDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReverseRefund(
        Guid id,
        Guid movementId,
        [FromBody] ReverseSupplierCreditRefundRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReverseSupplierCreditRefundCommand(
                    id,
                    movementId,
                    request.Reason,
                    request.EffectiveDate,
                    request.ClientRequestId
                ),
                ct
            )
        );
}

// ── Request DTOs — el Id del agregado (y del movimiento, si aplica) va en la ruta ───

/// <summary>Cuerpo de <see cref="SupplierCreditController.Apply"/>.</summary>
public sealed record ApplySupplierCreditRequest(
    Guid TargetPurchasePayableId,
    decimal Amount,
    Guid ClientRequestId
);

/// <summary>Cuerpo de <see cref="SupplierCreditController.ReverseApplication"/>.</summary>
public sealed record ReverseSupplierCreditApplicationRequest(
    Guid TargetPurchasePayableId,
    Guid ClientRequestId
);

/// <summary>Cuerpo de <see cref="SupplierCreditController.Refund"/>.</summary>
public sealed record RegisterSupplierCreditRefundRequest(
    Guid FinancialDestinationId,
    string PaymentMethodCode,
    decimal Amount,
    DateOnly EffectiveDate,
    string? ExternalReference,
    Guid ClientRequestId
);

/// <summary>Cuerpo de <see cref="SupplierCreditController.ReverseRefund"/>.</summary>
public sealed record ReverseSupplierCreditRefundRequest(
    string Reason,
    DateOnly EffectiveDate,
    Guid ClientRequestId
);
