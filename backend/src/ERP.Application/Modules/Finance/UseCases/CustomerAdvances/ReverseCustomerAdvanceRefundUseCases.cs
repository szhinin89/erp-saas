using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F5 — revierte una devolución que no llegó a ejecutarse: el dinero se
/// recuperó o nunca salió de la empresa. Importe, destino y medio salen de la transacción original; la
/// fecha efectiva es el día de negocio de la reversa (TA-07) y la fecha de la evidencia es un dato de
/// trazabilidad. Una devolución entregada al cliente no se revierte: se registra un nuevo anticipo.
/// </summary>
public sealed record ReverseCustomerAdvanceRefundCommand(
    Guid CustomerAdvanceId,
    Guid TransactionId,
    string Reason,
    string Reference,
    DateOnly EvidenceDate,
    /// <summary>Intención del cliente: una por reversa, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>Representación canónica V1 de la intención de reversa. Cambiar su forma = V2.</summary>
internal sealed record CustomerAdvanceRefundReversalIntentV1(
    string Operation,
    Guid CustomerAdvanceId,
    Guid TransactionId,
    string? Reason,
    string? Reference,
    DateOnly EvidenceDate
)
{
    public static string ComputeHash(ReverseCustomerAdvanceRefundCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceRefundReversalIntentV1(
                "CustomerAdvance.ReverseRefund",
                cmd.CustomerAdvanceId,
                cmd.TransactionId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reason),
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference),
                cmd.EvidenceDate
            )
        );
}

public sealed class ReverseCustomerAdvanceRefundCommandValidator
    : AbstractValidator<ReverseCustomerAdvanceRefundCommand>
{
    public ReverseCustomerAdvanceRefundCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerAdvanceId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo del reverso es obligatorio.")
            .MaximumLength(CustomerAdvanceMovement.ReasonMaxLen);
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("La referencia de la recuperación es obligatoria.")
            .MaximumLength(CustomerAdvanceMovement.ReferenceMaxLen);
        RuleFor(x => x.EvidenceDate)
            .NotEmpty()
            .WithMessage("La fecha de la evidencia es obligatoria.");
    }
}

/// <remarks>
/// Orden de locks: anticipo <c>FOR UPDATE</c> → turno <c>FOR UPDATE</c> → locks contables. El destino
/// se hereda y no se revalida su vigencia. El asiento se espeja por el reverso común (CA-F2).
/// </remarks>
public sealed class ReverseCustomerAdvanceRefundHandler
    : IRequestHandler<ReverseCustomerAdvanceRefundCommand, Result<CustomerAdvanceDto>>
{
    private const string CashMovementDescription = "Reversa de devolución de anticipo de cliente";

    private readonly ICustomerAdvanceRepository _advances;
    private readonly ICustomerAdvanceRefundTransactionRepository _transactions;
    private readonly CustomerAdvanceMoney _money;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;

    public ReverseCustomerAdvanceRefundHandler(
        ICustomerAdvanceRepository advances,
        ICustomerAdvanceRefundTransactionRepository transactions,
        IBusinessPartnerRepository partners,
        IPaymentMethodRepository paymentMethods,
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        ICashSessionRepository cashSessions,
        IAccountRepository accounts,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator databaseErrors,
        ICompanyClock clock,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _advances = advances;
        _transactions = transactions;
        _money = new(paymentMethods, bankAccounts, cashRegisters, cashSessions, accounts);
        _clock = clock;
        _t = t;
        _b = b;
        _u = u;
        _intent = new(advances, transactions, partners, uow, databaseErrors, t);
    }

    public Task<Result<CustomerAdvanceDto>> Handle(
        ReverseCustomerAdvanceRefundCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceRefundReversalIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => ReverseAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> ReverseAsync(
        ReverseCustomerAdvanceRefundCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var advance = await _advances.GetByIdForUpdateAsync(_t.TenantId, cmd.CustomerAdvanceId, ct);
        if (advance is null)
            return Result<CustomerAdvanceDto>.NotFound("El anticipo indicado no existe.");
        if (await _intent.ReplayFromAsync(advance, key, ct) is { } replay)
            return replay;

        var original = await _transactions.GetByIdAsync(advance.TenantId, cmd.TransactionId, ct);
        if (original is null || original.CustomerAdvanceId != advance.Id)
            return Result<CustomerAdvanceDto>.NotFound(
                "La devolución indicada no existe en este anticipo."
            );
        if (original.TransactionType != CustomerAdvanceRefundTransactionType.Refund)
            return Result<CustomerAdvanceDto>.ValidationFailure(
                "Solo se puede revertir una devolución."
            );
        var today = await _clock.TodayAsync(advance.CompanyId, advance.TenantId, ct);
        if (today < original.EffectiveDate)
            throw new DomainRuleViolationException(
                $"La reversa no puede ser anterior a la devolución del {original.EffectiveDate:yyyy-MM-dd}."
            );

        CustomerAdvanceApplicationChronology.EnsureEvidenceDate(
            cmd.EvidenceDate,
            original.EffectiveDate,
            today
        );

        var (session, sessionError) = original.CashRegisterId is { } cashRegisterId
            ? await _money.LockSessionAsync(
                advance.TenantId,
                advance.CompanyId,
                cashRegisterId,
                _u.UserId,
                outflow: null,
                ct
            )
            : default;
        if (sessionError is not null)
            return sessionError;

        // El agregado rechaza un anticipo cancelado y una segunda reversa.
        var movement = advance.ReverseRefund(
            original.CustomerAdvanceMovementId,
            new CustomerAdvanceCompensation(cmd.Reason, cmd.Reference, today, cmd.EvidenceDate),
            _b.BranchId,
            _u.UserId,
            key
        );
        var reversal = CustomerAdvanceRefundTransaction.CreateReversal(original, movement);
        if (session is not null)
        {
            var cashMovement = session.RecordMovement(
                CashMovementType.ManualIncome,
                reversal.Amount,
                CashMovementDescription,
                _u.UserId,
                CashReferenceType.CustomerAdvance,
                original.Id,
                CustomerAdvanceMoney.VisibleReference(reversal.Reference)
            );
            reversal.LinkCashMovement(session.Id, cashMovement.Id);
        }

        await _transactions.AddAsync(reversal, ct);
        await _advances.SaveChangesAsync(ct);
        return await _intent.SuccessAsync(advance, ct);
    }
}
