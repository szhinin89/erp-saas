using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.Payments;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// Fase 5.5.5.3 — registra un cobro (AR) aplicándolo contra una o más <c>SalesReceivable</c>.
/// Toda la lógica de negocio (balance, límites de saldo, transiciones de estado) vive en
/// <c>Payment</c>/<c>SalesReceivable</c> — este handler solo orquesta: carga, delega, persiste.
/// </summary>
public sealed record RegisterCollectionCommand(
    Guid CustomerId,
    decimal Amount,
    DateOnly PaymentDate,
    Guid? PaymentMethodId,
    string? Reference,
    IReadOnlyList<PaymentApplicationLineInput> Lines,
    /// <summary>
    /// ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — destino financiero (caja/banco) que
    /// recibió el cobro. Opcional: sin especificar, la contabilización sigue usando la cuenta
    /// fija de la PostingRule (comportamiento previo, sin cambios).
    /// </summary>
    Guid? FinancialDestinationId = null
) : IRequest<Result<PaymentDto>>, ICompanyScopedRequest;

/// <summary>Fase 5.5.5.3 — reversa un cobro ya aplicado y decrementa el saldo de cada CxC afectada.</summary>
public sealed record ReverseCollectionCommand(Guid PaymentId, string Reason)
    : IRequest<Result<PaymentDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class PaymentApplicationLineInputValidator
    : AbstractValidator<PaymentApplicationLineInput>
{
    public PaymentApplicationLineInputValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.AppliedAmount).GreaterThan(0);
    }
}

public sealed class RegisterCollectionCommandValidator
    : AbstractValidator<RegisterCollectionCommand>
{
    public RegisterCollectionCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("El cobro debe tener al menos una línea de aplicación.");
        RuleForEach(x => x.Lines).SetValidator(new PaymentApplicationLineInputValidator());
    }
}

public sealed class ReverseCollectionCommandValidator : AbstractValidator<ReverseCollectionCommand>
{
    public ReverseCollectionCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo del reverso es obligatorio.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class RegisterCollectionCommandHandler
    : IRequestHandler<RegisterCollectionCommand, Result<PaymentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly ISalesReceivableRepository _receivables;
    private readonly ICompanyFinancialDestinationRepository _financialDestinations;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public RegisterCollectionCommandHandler(
        IPaymentRepository payments,
        ISalesReceivableRepository receivables,
        ICompanyFinancialDestinationRepository financialDestinations,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _receivables = receivables;
        _financialDestinations = financialDestinations;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(
        RegisterCollectionCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — un destino financiero explícitamente
        // elegido debe existir, pertenecer a esta empresa y estar activo (a diferencia del caso
        // "sin especificar", que nunca bloquea el cobro — ver PostingRule fallback en el
        // traductor). El repositorio ya filtra por empresa activa (ForOperationalScope).
        if (cmd.FinancialDestinationId is { } financialDestinationId)
        {
            var destination = await _financialDestinations.GetByIdAsync(
                tenantId,
                financialDestinationId,
                ct
            );
            if (destination is null || !destination.IsActive)
                return Result<PaymentDto>.ValidationFailure(
                    "El destino financiero indicado no existe, no pertenece a esta empresa o está inactivo."
                );
        }

        Payment payment;
        try
        {
            payment = Payment.Create(
                tenantId,
                companyId,
                PaymentDirection.Collection,
                cmd.CustomerId,
                cmd.Amount,
                cmd.PaymentDate,
                cmd.PaymentMethodId,
                cmd.Reference,
                _u.UserId,
                cmd.FinancialDestinationId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }

        // Carga cada CxC referenciada una sola vez, incluso si varias líneas la referencian
        // (p. ej. aplicación repartida entre cuotas de la misma factura).
        var receivablesByDocId =
            new Dictionary<Guid, Domain.Modules.Sales.Entities.SalesReceivable>();
        foreach (var line in cmd.Lines)
        {
            if (!receivablesByDocId.ContainsKey(line.DocumentId))
            {
                var receivable = await _receivables.GetByIdAsync(tenantId, line.DocumentId, ct);
                if (receivable is null)
                    return Result<PaymentDto>.NotFound(
                        $"Cuenta por cobrar {line.DocumentId} no encontrada."
                    );
                receivablesByDocId[line.DocumentId] = receivable;
            }

            try
            {
                payment.AddApplicationLine(line.DocumentId, line.InstallmentId, line.AppliedAmount);
            }
            catch (ArgumentException ex)
            {
                return Result<PaymentDto>.ValidationFailure(ex.Message);
            }
        }

        try
        {
            payment.Apply(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }

        foreach (var line in cmd.Lines)
        {
            try
            {
                receivablesByDocId[line.DocumentId]
                    .RegisterCollection(line.AppliedAmount, _u.UserId);
            }
            catch (InvalidOperationException ex)
            {
                return Result<PaymentDto>.ValidationFailure(ex.Message);
            }
        }

        await _payments.AddAsync(payment, ct);
        await _payments.SaveChangesAsync(ct);

        return Result<PaymentDto>.Success(Map.ToDto(payment));
    }
}

public sealed class ReverseCollectionCommandHandler
    : IRequestHandler<ReverseCollectionCommand, Result<PaymentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly ISalesReceivableRepository _receivables;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ReverseCollectionCommandHandler(
        IPaymentRepository payments,
        ISalesReceivableRepository receivables,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _receivables = receivables;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(ReverseCollectionCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var payment = await _payments.GetByIdAsync(tenantId, companyId, cmd.PaymentId, ct);
        if (payment is null)
            return Result<PaymentDto>.NotFound("Pago no encontrado.");
        if (payment.Direction != PaymentDirection.Collection)
            return Result<PaymentDto>.ValidationFailure("El pago indicado no es un cobro.");

        try
        {
            payment.Reverse(_u.UserId, cmd.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }

        foreach (var line in payment.Lines)
        {
            var receivable = await _receivables.GetByIdAsync(
                tenantId,
                line.ReceivableId!.Value,
                ct
            );
            if (receivable is null)
                return Result<PaymentDto>.NotFound(
                    $"Cuenta por cobrar {line.ReceivableId} no encontrada."
                );

            try
            {
                receivable.ReverseCollection(line.AppliedAmount, _u.UserId);
            }
            catch (InvalidOperationException ex)
            {
                return Result<PaymentDto>.ValidationFailure(ex.Message);
            }
        }

        await _payments.SaveChangesAsync(ct);
        return Result<PaymentDto>.Success(Map.ToDto(payment));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static PaymentDto ToDto(Payment p) =>
        new(
            p.Id,
            p.Direction.ToString(),
            p.PartnerId,
            p.Amount,
            p.PaymentDate,
            p.PaymentMethodId,
            p.Reference,
            p.Status.ToString(),
            p.AppliedAtUtc,
            p.ReversedAtUtc,
            p.ReverseReason,
            p.Lines.Select(l => new PaymentApplicationLineDto(
                    l.Id,
                    l.ReceivableId,
                    l.PayableId,
                    l.InstallmentId,
                    l.AppliedAmount
                ))
                .ToList(),
            p.CreatedAt,
            p.UpdatedAt,
            p.FinancialDestinationId
        );
}
