using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Finance.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 8 — reversa un reembolso previamente registrado. Contrato deliberadamente angosto
/// (§6.4quinquies): nunca recibe destino/cuenta/método/importe/moneda — todos se heredan del
/// ingreso original congelado, nunca se revalidan vigentes.
/// </summary>
public sealed record ReverseSupplierCreditRefundCommand(
    Guid SupplierCreditId,
    Guid OriginalRefundTransactionId,
    string Reason,
    DateOnly EffectiveDate,
    Guid ClientRequestId
) : IRequest<Result<SupplierCreditRefundTransactionDto>>, ICompanyScopedRequest;

public sealed class ReverseSupplierCreditRefundValidator
    : AbstractValidator<ReverseSupplierCreditRefundCommand>
{
    public ReverseSupplierCreditRefundValidator()
    {
        RuleFor(x => x.SupplierCreditId).NotEmpty();
        RuleFor(x => x.OriginalRefundTransactionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de la reversa es obligatorio.");
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class ReverseSupplierCreditRefundHandler
    : IRequestHandler<
        ReverseSupplierCreditRefundCommand,
        Result<SupplierCreditRefundTransactionDto>
    >
{
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;
    private readonly ICashSessionRepository _cashSessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ReverseSupplierCreditRefundHandler(
        ISupplierCreditRepository creditRepo,
        ISupplierCreditRefundTransactionRepository txRepo,
        ICashSessionRepository cashSessionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditRepo = creditRepo;
        _txRepo = txRepo;
        _cashSessionRepo = cashSessionRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<SupplierCreditRefundTransactionDto>> Handle(
        ReverseSupplierCreditRefundCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // 1. Lock B por SupplierCreditId.
            await _creditRepo.AcquireLockAsync(tid, cmd.SupplierCreditId, ct);

            var credit = await _creditRepo.GetByIdAsync(tid, cmd.SupplierCreditId, ct);
            if (credit is null)
            {
                await _uow.RollbackAsync(ct);
                // SC-001
                return Result<SupplierCreditRefundTransactionDto>.NotFound(
                    "El crédito de proveedor indicado no existe."
                );
            }

            // ── Idempotencia (§16.2) ──
            var existingReversal = credit.Movements.FirstOrDefault(m =>
                m.ClientRequestId == cmd.ClientRequestId
                && m.MovementType == SupplierCreditMovementType.ReversalOfRefund
            );
            if (existingReversal is not null)
            {
                await _uow.RollbackAsync(ct);
                var expectedHash = ComputeReversePayloadHash(
                    cmd.SupplierCreditId,
                    cmd.OriginalRefundTransactionId,
                    cmd.Reason
                );
                if (existingReversal.RequestPayloadHash != expectedHash)
                    return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                        "Ya existe una solicitud de reversa con este identificador pero con datos distintos."
                    );

                var existingTx = await _txRepo.GetBySupplierCreditMovementIdAsync(
                    tid,
                    existingReversal.Id,
                    ct
                );
                return Result<SupplierCreditRefundTransactionDto>.Success(
                    RefundMap.ToDto(existingTx!)
                );
            }

            // 2. Cargar y bloquear (FOR SHARE) el REFUND_RECEIVED original.
            var original = await _txRepo.GetByIdForShareAsync(
                tid,
                cmd.OriginalRefundTransactionId,
                ct
            );
            if (original is null || original.SupplierCreditId != cmd.SupplierCreditId)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditRefundTransactionDto>.NotFound(
                    "La transacción de reembolso original no existe."
                );
            }
            if (original.TransactionTypeCode != RefundTransactionTypeCode.RefundReceived)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "Solo se puede revertir una transacción de tipo ingreso de reembolso."
                );
            }

            // 6. Si corresponde a caja: resolver la CashRegisterId heredada (campo estructural
            // inmutable del destino, §6.4ter) y bloquear (FOR SHARE) su CashSession activa —
            // nunca revalida el destino/cuenta vigentes (§6.4quinquies paso 5).
            Domain.Modules.Caja.Entities.CashSession? cashSession = null;
            if (
                original.DestinationTypeCodeSnapshot
                    == FinancialDestinationTypeCode.CashRegister.ToString()
                && original.CashSessionId.HasValue
            )
            {
                var originalSession = await _cashSessionRepo.GetByIdAsync(
                    tid,
                    original.CashSessionId.Value,
                    ct
                );
                if (originalSession is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierCreditRefundTransactionDto>.NotFound(
                        "La sesión de caja original no fue encontrada."
                    );
                }

                cashSession = await _cashSessionRepo.GetOpenByCashRegisterForShareAsync(
                    tid,
                    originalSession.CashRegisterId,
                    ct
                );
                if (cashSession is null)
                {
                    await _uow.RollbackAsync(ct);
                    // SC-027
                    return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                        "No existe una sesión de caja activa para revertir este reembolso."
                    );
                }
            }

            var hash = ComputeReversePayloadHash(
                cmd.SupplierCreditId,
                cmd.OriginalRefundTransactionId,
                cmd.Reason
            );

            // 3/7. Crear SupplierCreditMovement(ReversalOfRefund) — SC-011 guardado por dominio
            // (EnsureNotAlreadyReversed).
            Domain.Modules.Purchases.Entities.SupplierCreditMovement reversalMovement;
            try
            {
                reversalMovement = credit.ReverseRefund(
                    original.SupplierCreditMovementId,
                    uid,
                    cmd.ClientRequestId,
                    hash
                );
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // SC-011
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(ex.Message);
            }

            Guid? cashMovementId = null;
            if (cashSession is not null)
            {
                var cashMovement = cashSession.RecordMovement(
                    CashMovementType.ManualIncome,
                    original.Amount,
                    $"Reversa de reembolso de crédito a proveedor {credit.SupplierId}",
                    uid,
                    CashReferenceType.None
                );
                cashMovementId = cashMovement.Id;
            }

            // 4/5. Heredar TODOS los datos financieros congelados del original — nunca resueltos
            // de nuevo (§6.4quinquies).
            var reversalTransaction = SupplierCreditRefundTransaction.CreateReversal(
                original,
                reversalMovement.Id,
                cmd.Reason,
                cmd.EffectiveDate,
                uid,
                cmd.ClientRequestId,
                hash,
                cashSessionId: cashSession?.Id,
                cashMovementId: cashMovementId
            );

            await _txRepo.AddAsync(reversalTransaction, ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "El crédito de proveedor fue modificado concurrentemente. Intente nuevamente."
                );
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                await _uow.RollbackAsync(ct);
                // SC-011 (segunda reversa concurrente) / SC-006 (colisión cruzada de CRI).
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "Ya existe una reversa registrada para esta transacción de reembolso."
                );
            }

            await _uow.CommitAsync(ct);
            return Result<SupplierCreditRefundTransactionDto>.Success(
                RefundMap.ToDto(reversalTransaction)
            );
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Huella determinista (§16.2, diseño línea 785): SupplierCreditId+OriginalTransactionId+Reason.</summary>
    public static string ComputeReversePayloadHash(
        Guid supplierCreditId,
        Guid originalTransactionId,
        string reason
    )
    {
        var canonical = string.Join(
            "",
            "ReverseSupplierCreditRefund",
            supplierCreditId.ToString("D"),
            originalTransactionId.ToString("D"),
            reason.Trim()
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
