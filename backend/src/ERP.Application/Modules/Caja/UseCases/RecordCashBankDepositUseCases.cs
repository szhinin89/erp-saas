using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

/// <summary>
/// ACCOUNTING-12C — depósito del efectivo de la caja en una cuenta bancaria de la empresa.
/// Operación semántica propia (<c>CashMovementType.BankDeposit</c>), nunca un Retiro con motivo: el
/// banco destino es <see cref="CompanyBankAccountId"/>, no se deduce de un motivo genérico.
/// </summary>
public sealed record RecordCashBankDepositCommand(
    Guid CashSessionId,
    Guid CompanyBankAccountId,
    decimal Amount,
    string Description,
    string? ReferenceNumber = null,
    /// <summary>Intención del cliente (estable en reintentos). Obligatorio.</summary>
    Guid ClientRequestId = default
) : IRequest<Result<CashMovementDto>>, IBranchScopedRequest;

/// <summary>Representación canónica V1 de la intención de un depósito bancario. Cambiar su forma = V2.</summary>
internal sealed record CashBankDepositIntentV1(
    Guid CashSessionId,
    Guid CompanyBankAccountId,
    decimal Amount,
    string? Description,
    string? ReferenceNumber
)
{
    public static string ComputeHash(RecordCashBankDepositCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CashBankDepositIntentV1(
                cmd.CashSessionId,
                cmd.CompanyBankAccountId,
                cmd.Amount,
                CanonicalRequestFingerprint.NormalizeText(cmd.Description),
                CanonicalRequestFingerprint.NormalizeText(cmd.ReferenceNumber)
            )
        );
}

// ── Validator ──────────────────────────────────────────────────────────

public sealed class RecordCashBankDepositValidator : AbstractValidator<RecordCashBankDepositCommand>
{
    public RecordCashBankDepositValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CashSessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(x => x.CompanyBankAccountId)
            .NotEmpty()
            .WithMessage("La cuenta bancaria destino es obligatoria.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(CashMovement.DescriptionMaxLen)
            .WithMessage("La descripción es obligatoria.");
        RuleFor(x => x.ReferenceNumber).MaximumLength(CashMovement.ReferenceNumberMaxLen);
    }
}

// ── Handler ────────────────────────────────────────────────────────────

/// <summary>
/// Mismo patrón transaccional e idempotente que <see cref="RecordCashMovementHandler"/>: sesión FOR
/// UPDATE → intención re-buscada bajo el lock → validaciones → un único movimiento vinculado a la
/// intención. La obligación contable (Debe banco / Haber caja) se registra en la misma transacción;
/// su intento corre después del commit y un fallo contable nunca revierte el depósito.
/// </summary>
public sealed class RecordCashBankDepositHandler(
    ICashSessionRepository repo,
    ICompanyBankAccountRepository bankAccounts,
    IUnitOfWork uow,
    ICurrentTenant tenant,
    ICurrentBranch branch,
    ICurrentUser user
) : IRequestHandler<RecordCashBankDepositCommand, Result<CashMovementDto>>
{
    public async Task<Result<CashMovementDto>> Handle(RecordCashBankDepositCommand cmd, CancellationToken ct)
    {
        var key = new ClientRequestKey(cmd.ClientRequestId, CashBankDepositIntentV1.ComputeHash(cmd));

        var existing = await repo.GetMovementByClientRequestIdAsync(tenant.TenantId, key.Id, ct);
        if (existing is not null)
            return Replay(existing, key);

        await uow.BeginTransactionAsync(ct);
        try
        {
            var result = await RecordLockedAsync(cmd, key, ct);
            if (result.IsSuccess)
                await uow.CommitAsync(ct);
            else
                await uow.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await uow.RollbackAsync(ct);
            var raced = await repo.GetMovementByClientRequestIdAsync(tenant.TenantId, key.Id, ct);
            if (raced is null)
                throw;
            return Replay(raced, key);
        }
    }

    private async Task<Result<CashMovementDto>> RecordLockedAsync(
        RecordCashBankDepositCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var session = await repo.GetByIdForUpdateAsync(tenant.TenantId, cmd.CashSessionId, ct);
        if (session is null || session.BranchId != branch.BranchId)
            return Result<CashMovementDto>.NotFound("Sesión de caja no encontrada.");

        var registered = await repo.GetMovementByClientRequestIdAsync(tenant.TenantId, key.Id, ct);
        if (registered is not null)
            return Replay(registered, key);

        if (!session.IsControlledBy(user.UserId))
            return Result<CashMovementDto>.ValidationFailure(CashSessionOwnership.RejectionMessage(session));

        // Fail-closed: la cuenta bancaria debe ser de la empresa de la sesión real y estar activa.
        var bank = await bankAccounts.GetByIdAsync(tenant.TenantId, cmd.CompanyBankAccountId, ct);
        if (bank is null || bank.CompanyId != session.CompanyId)
            return Result<CashMovementDto>.ValidationFailure(
                "La cuenta bancaria seleccionada no existe o no pertenece a esta empresa."
            );
        if (!bank.IsActive)
            return Result<CashMovementDto>.ValidationFailure("La cuenta bancaria seleccionada está inactiva.");
        if (CashSessionAvailability.OverdraftMessage(session, cmd.Amount) is { } overdraft)
            return Result<CashMovementDto>.ValidationFailure(overdraft);

        var movement = session.RecordBankDeposit(bank.Id, cmd.Amount, cmd.Description, user.UserId, cmd.ReferenceNumber);
        movement.BindClientRequest(key);

        await repo.SaveChangesAsync(ct);
        return Result<CashMovementDto>.Success(ToDto(movement));
    }

    private Result<CashMovementDto> Replay(CashMovement existing, ClientRequestKey key) =>
        key.Matches(existing.RequestPayloadHash)
            ? Result<CashMovementDto>.Success(ToDto(existing))
            : Result<CashMovementDto>.Conflict(RecordCashMovementHandler.ClientRequestConflict);

    private CashMovementDto ToDto(CashMovement movement) =>
        new(
            movement.Id,
            movement.MovementType.ToString(),
            movement.Amount,
            movement.Description,
            movement.CreatedAt,
            movement.CreatedBy,
            user.FullName,
            movement.ReferenceType.ToString(),
            movement.ReferenceId,
            movement.ReferenceNumber,
            movement.ReasonId,
            movement.ReasonName
        );
}
