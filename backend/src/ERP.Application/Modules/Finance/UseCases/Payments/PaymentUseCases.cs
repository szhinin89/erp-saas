using ERP.Application.Common;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
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
    IReadOnlyList<PaymentApplicationLineInput> Lines
) : IRequest<Result<PaymentDto>>, ICompanyScopedRequest;

/// <summary>Fase 5.5.5.3 — registra un pago (AP) aplicándolo contra una o más <c>PurchasePayable</c>.</summary>
public sealed record RegisterPaymentCommand(
    Guid SupplierId,
    decimal Amount,
    DateOnly PaymentDate,
    Guid? PaymentMethodId,
    string? Reference,
    IReadOnlyList<PaymentApplicationLineInput> Lines
) : IRequest<Result<PaymentDto>>, ICompanyScopedRequest;

/// <summary>Fase 5.5.5.3 — reversa un cobro ya aplicado y decrementa el saldo de cada CxC afectada.</summary>
public sealed record ReverseCollectionCommand(Guid PaymentId, string Reason)
    : IRequest<Result<PaymentDto>>,
        ICompanyScopedRequest;

/// <summary>Fase 5.5.5.3 — reversa un pago ya aplicado y decrementa el saldo de cada CxP afectada.</summary>
public sealed record ReversePaymentCommand(Guid PaymentId, string Reason)
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

public sealed class RegisterPaymentCommandValidator : AbstractValidator<RegisterPaymentCommand>
{
    public RegisterPaymentCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("El pago debe tener al menos una línea de aplicación.");
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

public sealed class ReversePaymentCommandValidator : AbstractValidator<ReversePaymentCommand>
{
    public ReversePaymentCommandValidator()
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
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public RegisterCollectionCommandHandler(
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

    public async Task<Result<PaymentDto>> Handle(
        RegisterCollectionCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

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
                _u.UserId
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

public sealed class RegisterPaymentCommandHandler
    : IRequestHandler<RegisterPaymentCommand, Result<PaymentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly IPurchasePayableRepository _payables;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public RegisterPaymentCommandHandler(
        IPaymentRepository payments,
        IPurchasePayableRepository payables,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _payables = payables;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(RegisterPaymentCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        Payment payment;
        try
        {
            payment = Payment.Create(
                tenantId,
                companyId,
                PaymentDirection.Payment,
                cmd.SupplierId,
                cmd.Amount,
                cmd.PaymentDate,
                cmd.PaymentMethodId,
                cmd.Reference,
                _u.UserId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }

        var payablesByDocId =
            new Dictionary<Guid, Domain.Modules.Purchases.Entities.PurchasePayable>();
        foreach (var line in cmd.Lines)
        {
            if (!payablesByDocId.ContainsKey(line.DocumentId))
            {
                var payable = await _payables.GetByIdAsync(tenantId, line.DocumentId, ct);
                if (payable is null)
                    return Result<PaymentDto>.NotFound(
                        $"Cuenta por pagar {line.DocumentId} no encontrada."
                    );
                payablesByDocId[line.DocumentId] = payable;
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
                payablesByDocId[line.DocumentId].RegisterPayment(line.AppliedAmount, _u.UserId);
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

public sealed class ReversePaymentCommandHandler
    : IRequestHandler<ReversePaymentCommand, Result<PaymentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly IPurchasePayableRepository _payables;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ReversePaymentCommandHandler(
        IPaymentRepository payments,
        IPurchasePayableRepository payables,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _payables = payables;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(ReversePaymentCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var payment = await _payments.GetByIdAsync(tenantId, companyId, cmd.PaymentId, ct);
        if (payment is null)
            return Result<PaymentDto>.NotFound("Pago no encontrado.");
        if (payment.Direction != PaymentDirection.Payment)
            return Result<PaymentDto>.ValidationFailure(
                "El pago indicado no es un pago a proveedor."
            );

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
            var payable = await _payables.GetByIdAsync(tenantId, line.PayableId!.Value, ct);
            if (payable is null)
                return Result<PaymentDto>.NotFound(
                    $"Cuenta por pagar {line.PayableId} no encontrada."
                );

            try
            {
                payable.ReversePayment(line.AppliedAmount, _u.UserId);
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
            p.UpdatedAt
        );
}
