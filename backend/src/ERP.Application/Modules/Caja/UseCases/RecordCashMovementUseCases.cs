using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

public sealed record RecordCashMovementCommand(
    Guid CashSessionId,
    string MovementType,
    decimal Amount,
    string Description,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? ReferenceNumber = null)
    : IRequest<Result<CashMovementDto>>, IBranchScopedRequest;

// ── Validator ──────────────────────────────────────────────────────────

public sealed class RecordCashMovementValidator : AbstractValidator<RecordCashMovementCommand>
{
    public RecordCashMovementValidator()
    {
        RuleFor(x => x.CashSessionId).NotEmpty()
            .WithMessage("La sesión de caja es obligatoria.");
        RuleFor(x => x.MovementType).NotEmpty()
            .WithMessage("El tipo de movimiento es obligatorio.");
        RuleFor(x => x.Amount).GreaterThan(0)
            .WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Description).NotEmpty()
            .MaximumLength(CashMovement.DescriptionMaxLen)
            .WithMessage("La descripción es obligatoria.");
        RuleFor(x => x.ReferenceNumber).MaximumLength(CashMovement.ReferenceNumberMaxLen);
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class RecordCashMovementHandler : IRequestHandler<RecordCashMovementCommand, Result<CashMovementDto>>
{
    private readonly ICashSessionRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public RecordCashMovementHandler(
        ICashSessionRepository repo,
        ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo; _t = t; _u = u;
    }

    public async Task<Result<CashMovementDto>> Handle(RecordCashMovementCommand cmd, CancellationToken ct)
    {
        if (!Enum.TryParse<CashMovementType>(cmd.MovementType, true, out var movementType))
            return Result<CashMovementDto>.ValidationFailure(
                $"Tipo de movimiento '{cmd.MovementType}' no válido.");

        var referenceType = CashReferenceType.None;
        if (!string.IsNullOrWhiteSpace(cmd.ReferenceType)
            && !Enum.TryParse(cmd.ReferenceType, true, out referenceType))
            return Result<CashMovementDto>.ValidationFailure(
                $"Tipo de referencia '{cmd.ReferenceType}' no válido.");

        var session = await _repo.GetByIdAsync(_t.TenantId, cmd.CashSessionId, ct);
        if (session is null)
            return Result<CashMovementDto>.NotFound("Sesión de caja no encontrada.");

        try
        {
            var movement = session.RecordMovement(
                movementType, cmd.Amount, cmd.Description, _u.UserId,
                referenceType, cmd.ReferenceId, cmd.ReferenceNumber);

            await _repo.SaveChangesAsync(ct);

            return Result<CashMovementDto>.Success(new CashMovementDto(
                movement.Id, movement.MovementType.ToString(), movement.Amount,
                movement.Description, movement.CreatedAt, movement.CreatedBy,
                movement.ReferenceType.ToString(), movement.ReferenceId, movement.ReferenceNumber));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CashMovementDto>.ValidationFailure(ex.Message);
        }
    }
}
