using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F5 — anulación administrativa de un registro cuyo dinero nunca se recibió:
/// anticipo íntegro, sin caja y sin asiento publicado, con declaración expresa y referencia de
/// evidencia. No mueve Caja ni Bancos ni genera asientos.
/// </summary>
public sealed record CancelCustomerAdvanceCommand(
    Guid CustomerAdvanceId,
    string Reason,
    string Reference,
    /// <summary>Declaración expresa «el dinero no fue recibido». Obligatoria.</summary>
    bool MoneyNotReceived,
    /// <summary>Intención del cliente: una por anulación, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>Representación canónica V1 de la intención de anulación. Cambiar su forma = V2.</summary>
internal sealed record CustomerAdvanceCancellationIntentV1(
    string Operation,
    Guid CustomerAdvanceId,
    string? Reason,
    string? Reference,
    bool MoneyNotReceived
)
{
    public static string ComputeHash(CancelCustomerAdvanceCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceCancellationIntentV1(
                "CustomerAdvance.Cancel",
                cmd.CustomerAdvanceId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reason),
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference),
                cmd.MoneyNotReceived
            )
        );
}

public sealed class CancelCustomerAdvanceCommandValidator
    : AbstractValidator<CancelCustomerAdvanceCommand>
{
    public CancelCustomerAdvanceCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerAdvanceId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la anulación es obligatorio.")
            .MaximumLength(CustomerAdvanceMovement.ReasonMaxLen);
        RuleFor(x => x.Reference)
            .NotEmpty()
            .WithMessage("La referencia de la evidencia es obligatoria.")
            .MaximumLength(CustomerAdvanceMovement.ReferenceMaxLen);
        RuleFor(x => x.MoneyNotReceived)
            .Equal(true)
            .WithMessage("Debe declarar que el dinero no fue recibido.");
    }
}

/// <remarks>
/// Orden de locks: anticipo <c>FOR UPDATE</c> → locks contables. La comprobación del asiento publicado
/// y la cancelación de la obligación las hace Contabilidad en la misma transacción (CA-F2): si ya hay
/// asiento, toda la operación se revierte.
/// </remarks>
public sealed class CancelCustomerAdvanceHandler
    : IRequestHandler<CancelCustomerAdvanceCommand, Result<CustomerAdvanceDto>>
{
    private readonly ICustomerAdvanceRepository _advances;
    private readonly ICompanyClock _clock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;

    public CancelCustomerAdvanceHandler(
        ICustomerAdvanceRepository advances,
        ICustomerAdvanceRefundTransactionRepository transactions,
        IBusinessPartnerRepository partners,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator databaseErrors,
        ICompanyClock clock,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _advances = advances;
        _clock = clock;
        _t = t;
        _b = b;
        _u = u;
        _intent = new(advances, transactions, partners, uow, databaseErrors, t);
    }

    public Task<Result<CustomerAdvanceDto>> Handle(
        CancelCustomerAdvanceCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceCancellationIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => CancelAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> CancelAsync(
        CancelCustomerAdvanceCommand cmd,
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
                $"La anulación no puede ser anterior al ingreso del {advance.AdvanceDate:yyyy-MM-dd}."
            );

        advance.CancelAdministratively(
            cmd.MoneyNotReceived,
            new CustomerAdvanceCompensation(cmd.Reason, cmd.Reference, today),
            _b.BranchId,
            _u.UserId,
            key
        );

        await _advances.SaveChangesAsync(ct);
        return await _intent.SuccessAsync(advance, ct);
    }
}
