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
/// CUSTOMER-ADVANCES-V1 CA-F5 — revierte completo un ingreso que realmente se deshizo, con evidencia.
/// Exige el anticipo íntegro; importe y destino salen del ingreso original. En efectivo el dinero sale
/// del turno abierto de la caja original; en transferencia la evidencia es la referencia bancaria. Si
/// el dinero sigue en la empresa corresponde devolverlo.
/// </summary>
public sealed record ReverseCustomerAdvanceIncomeCommand(
    Guid CustomerAdvanceId,
    string Reason,
    string Reference,
    DateOnly EvidenceDate,
    /// <summary>Intención del cliente: una por reversa, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>Representación canónica V1 de la intención de reversa. Cambiar su forma = V2.</summary>
internal sealed record CustomerAdvanceIncomeReversalIntentV1(
    string Operation,
    Guid CustomerAdvanceId,
    string? Reason,
    string? Reference,
    DateOnly EvidenceDate
)
{
    public static string ComputeHash(ReverseCustomerAdvanceIncomeCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceIncomeReversalIntentV1(
                "CustomerAdvance.ReverseIncome",
                cmd.CustomerAdvanceId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reason),
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference),
                cmd.EvidenceDate
            )
        );
}

public sealed class ReverseCustomerAdvanceIncomeCommandValidator
    : AbstractValidator<ReverseCustomerAdvanceIncomeCommand>
{
    public ReverseCustomerAdvanceIncomeCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerAdvanceId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo del reverso es obligatorio.")
            .MaximumLength(CustomerAdvanceMovement.ReasonMaxLen);
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("La referencia de la evidencia es obligatoria.")
            .MaximumLength(CustomerAdvanceMovement.ReferenceMaxLen);
        RuleFor(x => x.EvidenceDate)
            .NotEmpty()
            .WithMessage("La fecha de la evidencia es obligatoria.");
    }
}

/// <remarks>
/// Orden de locks: anticipo <c>FOR UPDATE</c> → turno <c>FOR UPDATE</c> → locks contables. El asiento
/// del ingreso se espeja por el reverso común; sin asiento, la obligación pendiente se cancela.
/// </remarks>
public sealed class ReverseCustomerAdvanceIncomeHandler
    : IRequestHandler<ReverseCustomerAdvanceIncomeCommand, Result<CustomerAdvanceDto>>
{
    private const string CashMovementDescription = "Reversa del ingreso de anticipo de cliente";

    private readonly ICustomerAdvanceRepository _advances;
    private readonly CustomerAdvanceMoney _money;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;

    public ReverseCustomerAdvanceIncomeHandler(
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
        _money = new(paymentMethods, bankAccounts, cashRegisters, cashSessions, accounts);
        _clock = clock;
        _t = t;
        _b = b;
        _u = u;
        _intent = new(advances, transactions, partners, uow, databaseErrors, t);
    }

    public Task<Result<CustomerAdvanceDto>> Handle(
        ReverseCustomerAdvanceIncomeCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceIncomeReversalIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => ReverseAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> ReverseAsync(
        ReverseCustomerAdvanceIncomeCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var advance = await _advances.GetByIdForUpdateAsync(_t.TenantId, cmd.CustomerAdvanceId, ct);
        if (advance is null)
            return Result<CustomerAdvanceDto>.NotFound("El anticipo indicado no existe.");
        if (await _intent.ReplayFromAsync(advance, key, ct) is { } replay)
            return replay;

        var today = await _clock.TodayAsync(advance.CompanyId, advance.TenantId, ct);
        if (today < advance.AdvanceDate)
            throw new DomainRuleViolationException(
                $"La reversa no puede ser anterior al ingreso del {advance.AdvanceDate:yyyy-MM-dd}."
            );
        var received = advance.Movements.Single(m =>
            m.MovementType == CustomerAdvanceMovementType.Received
        );
        CustomerAdvanceApplicationChronology.EnsureEvidenceDate(
            cmd.EvidenceDate,
            received.EffectiveDate,
            today
        );

        var (session, sessionError) = advance.CashRegisterId is { } cashRegisterId
            ? await _money.LockSessionAsync(
                advance.TenantId,
                advance.CompanyId,
                cashRegisterId,
                _u.UserId,
                received.Amount,
                ct
            )
            : default;
        if (sessionError is not null)
            return sessionError;

        // El agregado exige el anticipo vigente e íntegro.
        var movement = advance.ReverseIncome(
            new CustomerAdvanceCompensation(cmd.Reason, cmd.Reference, today, cmd.EvidenceDate),
            _b.BranchId,
            _u.UserId,
            key
        );
        if (session is not null)
        {
            var cashMovement = session.RecordMovement(
                CashMovementType.ManualExpense,
                movement.Amount,
                CashMovementDescription,
                _u.UserId,
                CashReferenceType.CustomerAdvance,
                received.Id,
                CustomerAdvanceMoney.VisibleReference(movement.Reference)
            );
            advance.LinkCashMovement(movement.Id, session.Id, cashMovement.Id);
        }

        await _advances.SaveChangesAsync(ct);
        return await _intent.SuccessAsync(advance, ct);
    }
}
