using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTO ─────────────────────────────────────────────────────────────────

public sealed record SupplierCreditRefundTransactionDto(
    Guid Id,
    string TransactionTypeCode,
    Guid? OriginalTransactionId,
    Guid FinancialDestinationId,
    Guid AccountingAccountId,
    string PaymentMethodCode,
    decimal Amount,
    string CurrencyCode,
    DateOnly EffectiveDate,
    string? ExternalReference,
    string? Reason,
    Guid? CashSessionId,
    Guid? CashMovementId
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 8 — registra un reembolso de <c>SupplierCredit</c> hacia un destino financiero real
/// (banco o caja): Lock B + <c>FOR SHARE</c> de <c>CompanyFinancialDestination</c>+<c>Account</c>+
/// <c>CashSession</c> condicional (§6.4quater), idempotente (§16.2, fila <c>RegisterRefund</c>).
/// </summary>
public sealed record RegisterSupplierCreditRefundCommand(
    Guid SupplierCreditId,
    Guid FinancialDestinationId,
    string PaymentMethodCode,
    decimal Amount,
    DateOnly EffectiveDate,
    string? ExternalReference,
    Guid ClientRequestId
) : IRequest<Result<SupplierCreditRefundTransactionDto>>, ICompanyScopedRequest;

public sealed class RegisterSupplierCreditRefundValidator
    : AbstractValidator<RegisterSupplierCreditRefundCommand>
{
    public RegisterSupplierCreditRefundValidator()
    {
        RuleFor(x => x.SupplierCreditId).NotEmpty();
        RuleFor(x => x.FinancialDestinationId).NotEmpty();
        RuleFor(x => x.PaymentMethodCode).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class RegisterSupplierCreditRefundHandler
    : IRequestHandler<RegisterSupplierCreditRefundCommand, Result<SupplierCreditRefundTransactionDto>>
{
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;
    private readonly ICompanyFinancialDestinationRepository _destinationRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly ICashSessionRepository _cashSessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public RegisterSupplierCreditRefundHandler(
        ISupplierCreditRepository creditRepo,
        ISupplierCreditRefundTransactionRepository txRepo,
        ICompanyFinancialDestinationRepository destinationRepo,
        IAccountRepository accountRepo,
        IPaymentMethodRepository paymentMethodRepo,
        ICashSessionRepository cashSessionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditRepo = creditRepo;
        _txRepo = txRepo;
        _destinationRepo = destinationRepo;
        _accountRepo = accountRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _cashSessionRepo = cashSessionRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<SupplierCreditRefundTransactionDto>> Handle(
        RegisterSupplierCreditRefundCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // 1. Lock B por SupplierCreditId (§6.4quater paso 1).
            await _creditRepo.AcquireLockAsync(tid, cmd.SupplierCreditId, ct);

            // 2. Recargar y validar SupplierCredit.
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
            var existingRefund = credit.Movements.FirstOrDefault(m =>
                m.ClientRequestId == cmd.ClientRequestId
                && m.MovementType == SupplierCreditMovementType.Refund
            );
            if (existingRefund is not null)
            {
                await _uow.RollbackAsync(ct);
                var expectedHash = ComputeRegisterPayloadHash(
                    cmd.SupplierCreditId,
                    cmd.FinancialDestinationId,
                    cmd.PaymentMethodCode,
                    cmd.Amount,
                    credit.CurrencyCode,
                    cmd.EffectiveDate,
                    cmd.ExternalReference
                );
                if (existingRefund.RequestPayloadHash != expectedHash)
                    // SC-006
                    return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                        "Ya existe una solicitud de reembolso con este identificador pero con datos distintos."
                    );

                var existingTx = await _txRepo.GetBySupplierCreditMovementIdAsync(
                    tid,
                    existingRefund.Id,
                    ct
                );
                return Result<SupplierCreditRefundTransactionDto>.Success(RefundMap.ToDto(existingTx!));
            }

            // 3-4. Cargar y bloquear (FOR SHARE) CompanyFinancialDestination + validar.
            var destination = await _destinationRepo.GetByIdForShareAsync(
                tid,
                cmd.FinancialDestinationId,
                ct
            );
            if (destination is null || destination.CompanyId != credit.CompanyId)
            {
                await _uow.RollbackAsync(ct);
                // SC-020
                return Result<SupplierCreditRefundTransactionDto>.NotFound(
                    "El destino financiero indicado no existe."
                );
            }
            if (!destination.IsActive)
            {
                await _uow.RollbackAsync(ct);
                // SC-021
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "El destino financiero indicado no está activo."
                );
            }
            if (!string.Equals(destination.CurrencyCode, credit.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                await _uow.RollbackAsync(ct);
                // SC-025
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "La moneda del destino financiero no coincide con la del crédito de proveedor."
                );
            }

            // 5-6. Cargar y bloquear (FOR SHARE) Account + validar.
            var account = await _accountRepo.GetByIdForShareAsync(
                tid,
                destination.CompanyId,
                destination.AccountingAccountId,
                ct
            );
            if (account is null || !account.IsActive || !account.AllowsPosting)
            {
                await _uow.RollbackAsync(ct);
                // SC-024
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "La cuenta contable del destino financiero no admite contabilización."
                );
            }

            // 7. PaymentMethod activo + RequiresReference.
            var paymentMethod = await _paymentMethodRepo.GetByCodeAsync(tid, cmd.PaymentMethodCode, ct);
            if (paymentMethod is null || !paymentMethod.IsActive)
            {
                await _uow.RollbackAsync(ct);
                // SC-015
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "El método de pago indicado no está activo."
                );
            }
            if (paymentMethod.RequiresReference && string.IsNullOrWhiteSpace(cmd.ExternalReference))
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "El método de pago seleccionado exige una referencia externa."
                );
            }

            // 8. Si CASH_REGISTER: resolver y bloquear (FOR SHARE) CashSession activa.
            Domain.Modules.Caja.Entities.CashSession? cashSession = null;
            if (destination.DestinationTypeCode == FinancialDestinationTypeCode.CashRegister)
            {
                cashSession = await _cashSessionRepo.GetOpenByCashRegisterForShareAsync(
                    tid,
                    destination.CashRegisterId!.Value,
                    ct
                );
                if (cashSession is null)
                {
                    await _uow.RollbackAsync(ct);
                    // SC-027
                    return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                        "No existe una sesión de caja activa para el destino financiero indicado."
                    );
                }
            }

            var hash = ComputeRegisterPayloadHash(
                cmd.SupplierCreditId,
                cmd.FinancialDestinationId,
                cmd.PaymentMethodCode,
                cmd.Amount,
                credit.CurrencyCode,
                cmd.EffectiveDate,
                cmd.ExternalReference
            );

            // 9. Crear SupplierCreditMovement(Refund) — SC-003 (sobreaplicación) guardado por dominio.
            Domain.Modules.Purchases.Entities.SupplierCreditMovement movement;
            try
            {
                movement = credit.RegisterRefund(cmd.Amount, uid, cmd.ClientRequestId, hash);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // SC-003
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(ex.Message);
            }

            // CashMovement real (factory ya existente, esquema sin modificar, §24) — dentro de la
            // sesión ya bloqueada, antes de construir la transacción para poder congelar su Id.
            Guid? cashMovementId = null;
            if (cashSession is not null)
            {
                var cashMovement = cashSession.RecordMovement(
                    CashMovementType.ManualExpense,
                    cmd.Amount,
                    $"Reembolso de crédito a proveedor {credit.SupplierId}",
                    uid,
                    CashReferenceType.None
                );
                cashMovementId = cashMovement.Id;
            }

            var transaction = SupplierCreditRefundTransaction.CreateReceived(
                tid,
                credit.CompanyId,
                credit.SupplierId,
                credit.Id,
                movement.Id,
                destination.Id,
                account.Id,
                account.Code.ToString(),
                destination.Code,
                destination.Name,
                destination.DestinationTypeCode.ToString(),
                cmd.PaymentMethodCode,
                cmd.Amount,
                credit.CurrencyCode,
                cmd.EffectiveDate,
                uid,
                cmd.ClientRequestId,
                hash,
                externalReference: cmd.ExternalReference,
                cashSessionId: cashSession?.Id,
                cashMovementId: cashMovementId
            );

            await _txRepo.AddAsync(transaction, ct);

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
                // §16.2bis — colisión de ClientRequestId/relación 1:1 (SC-006/SC-029) por una causa
                // distinta al lock. Se rechaza de forma conservadora, sin intentar el snapshot
                // cacheado (correcto: nunca es un reintento legítimo de esta misma operación).
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditRefundTransactionDto>.ValidationFailure(
                    "Ya existe una solicitud con este identificador de idempotencia."
                );
            }

            await _uow.CommitAsync(ct);
            return Result<SupplierCreditRefundTransactionDto>.Success(RefundMap.ToDto(transaction));
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

    /// <summary>Huella determinista (§16.2, diseño línea 785): SupplierCreditId+FinancialDestinationId+PaymentMethodCode+Amount+CurrencyCode+EffectiveDate+ExternalReference normalizada.</summary>
    public static string ComputeRegisterPayloadHash(
        Guid supplierCreditId,
        Guid financialDestinationId,
        string paymentMethodCode,
        decimal amount,
        string currencyCode,
        DateOnly effectiveDate,
        string? externalReference
    )
    {
        var canonical = string.Join(
            "",
            "RegisterSupplierCreditRefund",
            supplierCreditId.ToString("D"),
            financialDestinationId.ToString("D"),
            paymentMethodCode.Trim().ToUpperInvariant(),
            amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            currencyCode.Trim().ToUpperInvariant(),
            effectiveDate.ToString("yyyy-MM-dd"),
            externalReference?.Trim() ?? ""
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}

// ── Mapping (compartido con ReverseSupplierCreditRefundUseCases) ────────

internal static class RefundMap
{
    public static SupplierCreditRefundTransactionDto ToDto(SupplierCreditRefundTransaction t) =>
        new(
            t.Id,
            t.TransactionTypeCode.ToString(),
            t.OriginalTransactionId,
            t.FinancialDestinationId,
            t.AccountingAccountId,
            t.PaymentMethodCode,
            t.Amount,
            t.CurrencyCode,
            t.EffectiveDate,
            t.ExternalReference,
            t.Reason,
            t.CashSessionId,
            t.CashMovementId
        );
}
