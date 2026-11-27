using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F4 (TA-04) — revierte completa una operación de aplicación, identificada
/// por su <c>ClientRequestId</c> original. CxC, cuotas e importes salen de los movimientos originales;
/// no se admite reversa parcial ni se mueve dinero. La fecha efectiva es el día de negocio de la
/// reversa (TA-07).
/// </summary>
public sealed record ReverseCustomerAdvanceApplicationCommand(
    Guid CustomerAdvanceId,
    Guid OperationId,
    string Reason,
    string Reference,
    /// <summary>Intención del cliente: una por reversa, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>Representación canónica V1 de la intención de reversa. Cambiar su forma = V2.</summary>
internal sealed record CustomerAdvanceApplicationReversalIntentV1(
    string Operation,
    Guid CustomerAdvanceId,
    Guid OperationId,
    string? Reason,
    string? Reference
)
{
    public static string ComputeHash(ReverseCustomerAdvanceApplicationCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceApplicationReversalIntentV1(
                "CustomerAdvance.ReverseApplication",
                cmd.CustomerAdvanceId,
                cmd.OperationId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reason),
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference)
            )
        );
}

public sealed class ReverseCustomerAdvanceApplicationCommandValidator
    : AbstractValidator<ReverseCustomerAdvanceApplicationCommand>
{
    public ReverseCustomerAdvanceApplicationCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerAdvanceId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo del reverso es obligatorio.")
            .MaximumLength(CustomerAdvanceMovement.ReasonMaxLen);
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("La referencia del reverso es obligatoria.")
            .MaximumLength(CustomerAdvanceMovement.ReferenceMaxLen);
    }
}

/// <remarks>
/// Mismo orden de locks que la aplicación: anticipo → CxC (Id ascendente) → locks contables. Si una
/// sola CxC no cumple TA-06, la reversa completa se rechaza sin efectos. El asiento se espeja por el
/// reverso común (CA-F2), que además exige período abierto para la fecha de la reversa.
/// </remarks>
public sealed class ReverseCustomerAdvanceApplicationHandler
    : IRequestHandler<ReverseCustomerAdvanceApplicationCommand, Result<CustomerAdvanceDto>>
{
    private readonly ICustomerAdvanceRepository _advances;
    private readonly ISalesReceivableRepository _receivables;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;

    public ReverseCustomerAdvanceApplicationHandler(
        ICustomerAdvanceRepository advances,
        ICustomerAdvanceRefundTransactionRepository transactions,
        ISalesReceivableRepository receivables,
        IBusinessPartnerRepository partners,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator databaseErrors,
        ICompanyClock clock,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _advances = advances;
        _receivables = receivables;
        _clock = clock;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _intent = new(advances, transactions, partners, uow, databaseErrors, t);
    }

    public Task<Result<CustomerAdvanceDto>> Handle(
        ReverseCustomerAdvanceApplicationCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceApplicationReversalIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => ReverseAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> ReverseAsync(
        ReverseCustomerAdvanceApplicationCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var advance = await _advances.GetByIdForUpdateAsync(_t.TenantId, cmd.CustomerAdvanceId, ct);
        if (advance is null)
            return Result<CustomerAdvanceDto>.NotFound("El anticipo indicado no existe.");
        if (await _intent.ReplayFromAsync(advance, key, ct) is { } replay)
            return replay;

        var originals = advance
            .Movements.Where(m =>
                m.MovementType == CustomerAdvanceMovementType.Application
                && m.ClientRequestId == cmd.OperationId
            )
            .OrderBy(m => m.LineNumber)
            .ToList();
        if (originals.Count == 0)
            return Result<CustomerAdvanceDto>.NotFound(
                "La aplicación indicada no existe en este anticipo."
            );

        var receivables = await _receivables.GetByIdsForUpdateAsync(
            _t.TenantId,
            originals.Select(m => m.ReceivableId!.Value).ToList(),
            ct
        );
        foreach (var group in originals.GroupBy(m => m.ReceivableId!.Value))
        {
            if (!receivables.TryGetValue(group.Key, out var receivable))
                return Result<CustomerAdvanceDto>.NotFound(
                    $"Cuenta por cobrar {group.Key} no encontrada."
                );
            EnsureCanRestore(receivable, group.Sum(m => m.Amount));
        }

        var today = await _clock.TodayAsync(_c.CompanyId, _t.TenantId, ct);
        // El agregado rechaza una segunda reversa de la misma operación.
        advance.ReverseApplication(
            cmd.OperationId,
            new CustomerAdvanceCompensation(cmd.Reason, cmd.Reference, today),
            _b.BranchId,
            _u.UserId,
            key
        );
        foreach (var original in originals)
            receivables[original.ReceivableId!.Value]
                .ReverseCollection(original.Amount, _u.UserId, original.InstallmentId);

        await _advances.SaveChangesAsync(ct);
        return await _intent.SuccessAsync(advance, ct);
    }

    /// <summary>TA-06: CxC vigente, con cobrado suficiente y cuotas que cuadran.</summary>
    private static void EnsureCanRestore(SalesReceivable receivable, decimal amount)
    {
        if (receivable.Status == "cancelled")
            throw new DomainRuleViolationException(
                $"La cuenta por cobrar {receivable.Id} está anulada: no admite revertir el anticipo."
            );
        if (amount > receivable.PaidAmount)
            throw new DomainRuleViolationException(
                $"Operaciones posteriores modificaron el saldo cobrado de la cuenta por cobrar {receivable.Id}."
            );
        CustomerAdvanceReceivableIntegrity.EnsureReversible(receivable);
    }
}
