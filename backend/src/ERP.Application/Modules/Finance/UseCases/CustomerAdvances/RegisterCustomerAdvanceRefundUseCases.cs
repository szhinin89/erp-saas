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
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F5 — devuelve al cliente, total o parcialmente, el saldo disponible del
/// anticipo: efectivo desde el turno de una caja o transferencia contra una cuenta bancaria, que puede
/// ser un destino distinto del ingreso. Lo aplicado a CxC no se devuelve: primero se revierte.
/// </summary>
public sealed record RegisterCustomerAdvanceRefundCommand(
    Guid CustomerAdvanceId,
    decimal Amount,
    DateOnly EffectiveDate,
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    string Reference,
    string Reason,
    /// <summary>Intención del cliente: una por devolución, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>Representación canónica V1 de la intención de devolución. Cambiar su forma = V2.</summary>
internal sealed record CustomerAdvanceRefundIntentV1(
    string Operation,
    Guid CustomerAdvanceId,
    decimal Amount,
    DateOnly EffectiveDate,
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    string? Reference,
    string? Reason
)
{
    public static string ComputeHash(RegisterCustomerAdvanceRefundCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceRefundIntentV1(
                "CustomerAdvance.Refund",
                cmd.CustomerAdvanceId,
                cmd.Amount,
                cmd.EffectiveDate,
                cmd.PaymentMethodId,
                cmd.CompanyBankAccountId,
                cmd.CashRegisterId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference),
                CanonicalRequestFingerprint.NormalizeText(cmd.Reason)
            )
        );
}

public sealed class RegisterCustomerAdvanceRefundCommandValidator
    : AbstractValidator<RegisterCustomerAdvanceRefundCommand>
{
    public RegisterCustomerAdvanceRefundCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerAdvanceId).NotEmpty();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .Must(amount => decimal.Round(amount, FiscalPrecision.TaxAmount) == amount)
            .WithMessage("El monto excede la precisión monetaria permitida.");
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("La referencia de ejecución es obligatoria.")
            .MaximumLength(CustomerAdvanceMovement.ReferenceMaxLen);
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la devolución es obligatorio.")
            .MaximumLength(CustomerAdvanceMovement.ReasonMaxLen);
        RuleFor(x => x)
            .Must(x => x.CompanyBankAccountId is not null ^ x.CashRegisterId is not null)
            .WithMessage("Debe especificar exactamente una cuenta bancaria o una caja destino.");
    }
}

/// <remarks>
/// Orden de locks: anticipo <c>FOR UPDATE</c> → destino → cuenta → turno → locks contables. El saldo
/// se decide con el libro recargado bajo el lock. Movimiento, transacción, caja, auditoría,
/// obligación contable y outbox se confirman juntos o no queda nada.
/// </remarks>
public sealed class RegisterCustomerAdvanceRefundHandler
    : IRequestHandler<RegisterCustomerAdvanceRefundCommand, Result<CustomerAdvanceDto>>
{
    private const string CashMovementDescription = "Devolución de anticipo de cliente";

    private readonly ICustomerAdvanceRepository _advances;
    private readonly ICustomerAdvanceRefundTransactionRepository _transactions;
    private readonly CustomerAdvanceMoney _money;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;

    public RegisterCustomerAdvanceRefundHandler(
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
        RegisterCustomerAdvanceRefundCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceRefundIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => RefundAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> RefundAsync(
        RegisterCustomerAdvanceRefundCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var advance = await _advances.GetByIdForUpdateAsync(_t.TenantId, cmd.CustomerAdvanceId, ct);
        if (advance is null)
            return Result<CustomerAdvanceDto>.NotFound("El anticipo indicado no existe.");
        if (await _intent.ReplayFromAsync(advance, key, ct) is { } replay)
            return replay;
        await EnsureDateAsync(advance, cmd.EffectiveDate, ct);

        var target = new CustomerAdvanceMoneyTarget(
            cmd.PaymentMethodId,
            cmd.CompanyBankAccountId,
            cmd.CashRegisterId,
            cmd.Reference
        );
        if (
            await _money.ValidateTargetAsync(advance.TenantId, advance.CompanyId, target, ct) is
            { } targetError
        )
            return targetError;

        var (session, sessionError) = cmd.CashRegisterId is { } cashRegisterId
            ? await _money.LockSessionAsync(
                advance.TenantId,
                advance.CompanyId,
                cashRegisterId,
                _u.UserId,
                cmd.Amount,
                ct
            )
            : default;
        if (sessionError is not null)
            return sessionError;

        // El agregado valida estado y saldo disponible antes de tocar su libro.
        var movement = advance.RegisterRefund(
            cmd.Amount,
            new CustomerAdvanceCompensation(cmd.Reason, cmd.Reference, cmd.EffectiveDate),
            _b.BranchId,
            _u.UserId,
            key
        );
        var transaction = CustomerAdvanceRefundTransaction.CreateRefund(
            advance,
            movement,
            cmd.PaymentMethodId,
            cmd.CompanyBankAccountId,
            cmd.CashRegisterId
        );
        if (session is not null)
        {
            // Movimiento interno sin motivo: lo contabiliza el anticipo, nunca Caja.
            var cashMovement = session.RecordMovement(
                CashMovementType.ManualExpense,
                transaction.Amount,
                CashMovementDescription,
                _u.UserId,
                CashReferenceType.CustomerAdvance,
                transaction.Id,
                CustomerAdvanceMoney.VisibleReference(transaction.Reference)
            );
            transaction.LinkCashMovement(session.Id, cashMovement.Id);
        }

        await _transactions.AddAsync(transaction, ct);
        await _advances.SaveChangesAsync(ct);
        return await _intent.SuccessAsync(advance, ct);
    }

    /// <summary>ADR-052 §17: entre la fecha del anticipo y el día de negocio de la empresa.</summary>
    private async Task EnsureDateAsync(
        CustomerAdvance advance,
        DateOnly effectiveDate,
        CancellationToken ct
    )
    {
        var today = await _clock.TodayAsync(advance.CompanyId, advance.TenantId, ct);
        if (effectiveDate > today)
            throw new DomainRuleViolationException(
                $"La fecha de la devolución {effectiveDate:yyyy-MM-dd} es posterior al día de negocio {today:yyyy-MM-dd}."
            );
        if (effectiveDate < advance.AdvanceDate)
            throw new DomainRuleViolationException(
                $"La fecha de la devolución {effectiveDate:yyyy-MM-dd} es anterior a la fecha del anticipo {advance.AdvanceDate:yyyy-MM-dd}."
            );
    }
}
