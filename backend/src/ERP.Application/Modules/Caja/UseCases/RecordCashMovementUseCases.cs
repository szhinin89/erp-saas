using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — <see cref="ReasonId"/> es obligatorio: este comando ahora
/// sirve EXCLUSIVAMENTE para movimientos manuales (Opening/SaleIncome/SaleRefund se rechazan en el
/// handler — esos tipos nunca deben crearse por esta vía genérica, solo por
/// <c>CashSession.Open</c>/<c>SalesInvoiceAuthorizedHandler</c>/<c>SalesReturnRefundHandler</c>).
/// </summary>
public sealed record RecordCashMovementCommand(
    Guid CashSessionId,
    string MovementType,
    Guid ReasonId,
    decimal Amount,
    string Description,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? ReferenceNumber = null
) : IRequest<Result<CashMovementDto>>, IBranchScopedRequest;

// ── Validator ──────────────────────────────────────────────────────────

public sealed class RecordCashMovementValidator : AbstractValidator<RecordCashMovementCommand>
{
    public RecordCashMovementValidator()
    {
        RuleFor(x => x.CashSessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(x => x.MovementType)
            .NotEmpty()
            .WithMessage("El tipo de movimiento es obligatorio.");
        RuleFor(x => x.ReasonId).NotEmpty().WithMessage("El motivo es obligatorio.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(CashMovement.DescriptionMaxLen)
            .WithMessage("La descripción es obligatoria.");
        RuleFor(x => x.ReferenceNumber).MaximumLength(CashMovement.ReferenceNumberMaxLen);
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class RecordCashMovementHandler
    : IRequestHandler<RecordCashMovementCommand, Result<CashMovementDto>>
{
    /// <summary>
    /// TREASURY-CASH-MANUAL-MOVEMENTS-01 — únicos tipos que este comando genérico puede crear.
    /// Opening solo lo crea <c>CashSession.Open</c>; SaleIncome/SaleRefund solo los crean
    /// <c>SalesInvoiceAuthorizedHandler</c>/<c>SalesReturnRefundHandler</c> a partir de una venta o
    /// devolución real autorizada — permitirlos aquí abriría un hueco para inflar/desinflar el
    /// efectivo esperado sin que exista la venta/devolución detrás.
    /// </summary>
    private static readonly CashMovementType[] AllowedManualTypes =
    [
        CashMovementType.ManualIncome,
        CashMovementType.ManualExpense,
        CashMovementType.Withdrawal,
    ];

    private readonly ICashSessionRepository _repo;
    private readonly ICashMovementReasonRepository _reasonRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public RecordCashMovementHandler(
        ICashSessionRepository repo,
        ICashMovementReasonRepository reasonRepo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _repo = repo;
        _reasonRepo = reasonRepo;
        _t = t;
        _b = b;
        _u = u;
    }

    public async Task<Result<CashMovementDto>> Handle(
        RecordCashMovementCommand cmd,
        CancellationToken ct
    )
    {
        if (!Enum.TryParse<CashMovementType>(cmd.MovementType, true, out var movementType))
            return Result<CashMovementDto>.ValidationFailure(
                $"Tipo de movimiento '{cmd.MovementType}' no válido."
            );

        if (!AllowedManualTypes.Contains(movementType))
            return Result<CashMovementDto>.ValidationFailure(
                "Este tipo de movimiento no se puede registrar manualmente — solo Ingreso manual, Egreso manual o Retiro."
            );

        var referenceType = CashReferenceType.None;
        if (
            !string.IsNullOrWhiteSpace(cmd.ReferenceType)
            && !Enum.TryParse(cmd.ReferenceType, true, out referenceType)
        )
            return Result<CashMovementDto>.ValidationFailure(
                $"Tipo de referencia '{cmd.ReferenceType}' no válido."
            );

        var session = await _repo.GetByIdAsync(_t.TenantId, cmd.CashSessionId, ct);
        if (session is null || session.BranchId != _b.BranchId)
            return Result<CashMovementDto>.NotFound("Sesión de caja no encontrada.");

        // Fail-closed: la búsqueda ya filtra por Tenant+Company de la sesión — un motivo de otro
        // tenant o de otra empresa (aunque exista con ese Id) llega aquí como null, exactamente
        // igual que "no existe". Nunca se usa un CompanyId ambient distinto del de la sesión real.
        var reason = await _reasonRepo.GetByIdAsync(_t.TenantId, session.CompanyId, cmd.ReasonId, ct);
        if (reason is null)
            return Result<CashMovementDto>.ValidationFailure(
                "El motivo seleccionado no existe o no pertenece a esta empresa."
            );
        if (!reason.IsActive)
            return Result<CashMovementDto>.ValidationFailure(
                "El motivo seleccionado está inactivo."
            );
        if (reason.MovementType != movementType)
            return Result<CashMovementDto>.ValidationFailure(
                "El motivo seleccionado no corresponde a este tipo de movimiento."
            );

        try
        {
            var movement = session.RecordMovement(
                movementType,
                cmd.Amount,
                cmd.Description,
                _u.UserId,
                referenceType,
                cmd.ReferenceId,
                cmd.ReferenceNumber,
                reason.Id,
                reason.Name
            );

            await _repo.SaveChangesAsync(ct);

            return Result<CashMovementDto>.Success(
                new CashMovementDto(
                    movement.Id,
                    movement.MovementType.ToString(),
                    movement.Amount,
                    movement.Description,
                    movement.CreatedAt,
                    movement.CreatedBy,
                    _u.FullName,
                    movement.ReferenceType.ToString(),
                    movement.ReferenceId,
                    movement.ReferenceNumber,
                    movement.ReasonId,
                    movement.ReasonName
                )
            );
        }
        catch (InvalidOperationException ex)
        {
            return Result<CashMovementDto>.ValidationFailure(ex.Message);
        }
    }
}
