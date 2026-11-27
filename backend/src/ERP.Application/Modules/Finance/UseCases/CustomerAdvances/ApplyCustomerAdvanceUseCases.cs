using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>Monto a aplicar a una CxC (y opcionalmente a una de sus cuotas).</summary>
public sealed record CustomerAdvanceApplicationInput(
    Guid ReceivableId,
    Guid? InstallmentId,
    decimal Amount
);

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F4 — aplica el anticipo, total o parcialmente, a una o varias CxC del
/// mismo cliente y empresa. No mueve dinero: reduce cada CxC con
/// <c>SalesReceivable.RegisterCollection</c>, sin <c>Payment</c> ni movimiento de caja. La sucursal
/// activa solo registra el origen (CA-INV-05).
/// </summary>
public sealed record ApplyCustomerAdvanceCommand(
    Guid CustomerAdvanceId,
    DateOnly EffectiveDate,
    IReadOnlyList<CustomerAdvanceApplicationInput> Lines,
    /// <summary>Intención del cliente: una por aplicación, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>
/// Representación canónica V1 de la intención de aplicación (ADR-052 §5): tipo de operación,
/// anticipo y líneas en su orden (que fija la numeración). Cambiar su forma = V2.
/// </summary>
internal sealed record CustomerAdvanceApplicationIntentV1(
    string Operation,
    Guid CustomerAdvanceId,
    DateOnly EffectiveDate,
    IReadOnlyList<CustomerAdvanceApplicationInput> Lines
)
{
    public static string ComputeHash(ApplyCustomerAdvanceCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceApplicationIntentV1(
                "CustomerAdvance.Apply",
                cmd.CustomerAdvanceId,
                cmd.EffectiveDate,
                cmd.Lines
            )
        );
}

public sealed class CustomerAdvanceApplicationInputValidator
    : AbstractValidator<CustomerAdvanceApplicationInput>
{
    public CustomerAdvanceApplicationInputValidator()
    {
        RuleFor(x => x.ReceivableId).NotEmpty();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .Must(amount => decimal.Round(amount, FiscalPrecision.TaxAmount) == amount)
            .WithMessage("El monto excede la precisión monetaria permitida.");
    }
}

public sealed class ApplyCustomerAdvanceCommandValidator
    : AbstractValidator<ApplyCustomerAdvanceCommand>
{
    /// <summary>Tope que impone la numeración de líneas del libro.</summary>
    public const int MaxLines = short.MaxValue;

    public ApplyCustomerAdvanceCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerAdvanceId).NotEmpty();
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("La aplicación debe tener al menos una línea.")
            .Must(lines => lines is null || lines.Count <= MaxLines)
            .WithMessage($"La aplicación admite como máximo {MaxLines} líneas.");
        RuleForEach(x => x.Lines).SetValidator(new CustomerAdvanceApplicationInputValidator());
    }
}

/// <remarks>
/// ADR-052 §7 — orden de locks: anticipo <c>FOR UPDATE</c> → CxC <c>FOR UPDATE</c> en Id ascendente →
/// locks contables. Todo se decide con los saldos recargados bajo esos locks; una línea inválida
/// rechaza la operación completa sin efectos (CA-INV-10). La cronología
/// (<see cref="CustomerAdvanceApplicationChronology"/>) se valida después del replay: un reintento de
/// una aplicación ya aceptada no se vuelve a evaluar.
/// </remarks>
public sealed class ApplyCustomerAdvanceHandler
    : IRequestHandler<ApplyCustomerAdvanceCommand, Result<CustomerAdvanceDto>>
{
    private readonly ICustomerAdvanceRepository _advances;
    private readonly ISalesReceivableRepository _receivables;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;
    private readonly CustomerAdvanceApplicationChronology _chronology;

    public ApplyCustomerAdvanceHandler(
        ICustomerAdvanceRepository advances,
        ICustomerAdvanceRefundTransactionRepository transactions,
        ISalesReceivableRepository receivables,
        IBusinessPartnerRepository partners,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator databaseErrors,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        ISalesInvoiceRepository invoices,
        IOpeningBalanceConstraintsReader openingBalance,
        ICompanyClock clock
    )
    {
        _advances = advances;
        _receivables = receivables;
        _t = t;
        _b = b;
        _u = u;
        _intent = new(advances, transactions, partners, uow, databaseErrors, t);
        _chronology = new(invoices, openingBalance, clock);
    }

    public Task<Result<CustomerAdvanceDto>> Handle(
        ApplyCustomerAdvanceCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceApplicationIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => ApplyAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> ApplyAsync(
        ApplyCustomerAdvanceCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var advance = await _advances.GetByIdForUpdateAsync(_t.TenantId, cmd.CustomerAdvanceId, ct);
        if (advance is null)
            return Result<CustomerAdvanceDto>.NotFound("El anticipo indicado no existe.");
        // El reintento que esperó el lock responde con lo que ya registró el ganador.
        if (await _intent.ReplayFromAsync(advance, key, ct) is { } replay)
            return replay;

        var receivables = await _receivables.GetByIdsForUpdateAsync(
            _t.TenantId,
            cmd.Lines.Select(l => l.ReceivableId).ToList(),
            ct
        );
        var lines = new List<CustomerAdvanceApplicationLine>(cmd.Lines.Count);
        foreach (var line in cmd.Lines)
        {
            receivables.TryGetValue(line.ReceivableId, out var receivable);
            var rejection = ReceivableApplicationPolicy.Validate(
                receivable,
                line.ReceivableId,
                line.InstallmentId,
                line.Amount,
                advance.CustomerId,
                "del anticipo"
            );
            if (rejection is { IsNotFound: true })
                return Result<CustomerAdvanceDto>.NotFound(rejection.Message);
            if (rejection is not null)
                throw new DomainRuleViolationException(rejection.Message);
            lines.Add(new(receivable!, line.InstallmentId, line.Amount));
        }

        await _chronology.EnsureAsync(advance, receivables.Values.ToList(), cmd.EffectiveDate, ct);

        // El agregado valida estado, saldo disponible, empresa y cliente antes de tocar su libro.
        advance.Apply(lines, cmd.EffectiveDate, _b.BranchId, _u.UserId, key);
        // En el orden de las líneas: la CxC valida cancelación, saldo y acumulado de cada una.
        foreach (var line in lines)
            line.Receivable.RegisterCollection(line.Amount, _u.UserId, line.InstallmentId);
        // TA-06 — nunca se crea una aplicación que luego no pueda revertirse.
        foreach (var receivable in receivables.Values)
            CustomerAdvanceReceivableIntegrity.EnsureReversible(receivable);

        await _advances.SaveChangesAsync(ct);
        return await _intent.SuccessAsync(advance, ct);
    }
}
