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
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public RegisterPaymentCommandHandler(
        IPaymentRepository payments,
        IPurchasePayableRepository payables,
        IPurchaseReturnRepository purchaseReturnRepo,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _payables = payables;
        _purchaseReturnRepo = purchaseReturnRepo;
        _uow = uow;
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

        // Fase 3 (P0-02, Remediación transaccional 02) — transacción explícita + Lock A por cada
        // PurchaseInvoiceId distinto involucrado, en orden ascendente de Guid como texto (§15.4).
        // El comando solo conoce PurchasePayable.Id por línea (no PurchaseInvoiceId): antes del
        // lock solo se descubre el PurchaseInvoiceId dueño de cada PurchasePayable, vía una
        // proyección SIN TRACKING (GetPurchaseInvoiceIdAsync) — nunca se rastrea la entidad en
        // este paso, para que la recarga posterior (GetByIdAsync, después del lock) sea garantía
        // de lectura fresca real y no la misma instancia servida por el identity map de EF Core.
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseInvoiceIdByDocId = new Dictionary<Guid, Guid>();
            foreach (var line in cmd.Lines)
            {
                if (purchaseInvoiceIdByDocId.ContainsKey(line.DocumentId))
                    continue;

                var purchaseInvoiceId = await _payables.GetPurchaseInvoiceIdAsync(
                    tenantId,
                    line.DocumentId,
                    ct
                );
                if (purchaseInvoiceId is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PaymentDto>.NotFound(
                        $"Cuenta por pagar {line.DocumentId} no encontrada."
                    );
                }
                purchaseInvoiceIdByDocId[line.DocumentId] = purchaseInvoiceId.Value;
            }

            foreach (
                var purchaseInvoiceId in purchaseInvoiceIdByDocId
                    .Values.Distinct()
                    .OrderBy(id => id.ToString())
            )
            {
                await _purchaseReturnRepo.AcquireFinancialLockAsync(
                    tenantId,
                    purchaseInvoiceId,
                    ct
                );
            }

            // ── Recarga autoritativa — primera vez que se rastrea cada PurchasePayable,
            // ya dentro del lock: garantizadamente fresca. ──────────────────
            var payablesByDocId =
                new Dictionary<Guid, Domain.Modules.Purchases.Entities.PurchasePayable>();
            foreach (var line in cmd.Lines)
            {
                if (payablesByDocId.ContainsKey(line.DocumentId))
                    continue;

                var payable = await _payables.GetByIdAsync(tenantId, line.DocumentId, ct);
                if (payable is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PaymentDto>.NotFound(
                        $"Cuenta por pagar {line.DocumentId} no encontrada."
                    );
                }
                payablesByDocId[line.DocumentId] = payable;
            }

            foreach (var line in cmd.Lines)
            {
                try
                {
                    payment.AddApplicationLine(
                        line.DocumentId,
                        line.InstallmentId,
                        line.AppliedAmount
                    );
                }
                catch (ArgumentException ex)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PaymentDto>.ValidationFailure(ex.Message);
                }
            }

            payment.Apply(_u.UserId);

            foreach (var line in cmd.Lines)
            {
                payablesByDocId[line.DocumentId].RegisterPayment(line.AppliedAmount, _u.UserId);
            }

            await _payments.AddAsync(payment, ct);
            await _payments.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return Result<PaymentDto>.Success(Map.ToDto(payment));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
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
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ReversePaymentCommandHandler(
        IPaymentRepository payments,
        IPurchasePayableRepository payables,
        IPurchaseReturnRepository purchaseReturnRepo,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _payables = payables;
        _purchaseReturnRepo = purchaseReturnRepo;
        _uow = uow;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(ReversePaymentCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // Carga del agregado propio (por su Id, dado directamente por el comando) fuera de
        // transacción — mismo criterio que la carga inicial en AuthorizeSalesReturnUseCases.
        var payment = await _payments.GetByIdAsync(tenantId, companyId, cmd.PaymentId, ct);
        if (payment is null)
            return Result<PaymentDto>.NotFound("Pago no encontrado.");
        if (payment.Direction != PaymentDirection.Payment)
            return Result<PaymentDto>.ValidationFailure(
                "El pago indicado no es un pago a proveedor."
            );

        // Fase 3 (P0-02, Remediación transaccional 02) — transacción explícita + Lock A por cada
        // PurchaseInvoiceId distinto involucrado, en orden ascendente de Guid como texto (§15.4).
        // payment.Lines ya contiene los PurchasePayable.Id afectados (poblados al aplicar el pago,
        // disponibles sin consulta adicional) — antes del lock solo se descubre el
        // PurchaseInvoiceId dueño de cada uno, vía una proyección SIN TRACKING
        // (GetPurchaseInvoiceIdAsync); la recarga autoritativa de cada PurchasePayable ocurre
        // recién después del lock, garantizando lectura fresca real.
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseInvoiceIdByPayableId = new Dictionary<Guid, Guid>();
            foreach (var line in payment.Lines)
            {
                var payableId = line.PayableId!.Value;
                if (purchaseInvoiceIdByPayableId.ContainsKey(payableId))
                    continue;

                var purchaseInvoiceId = await _payables.GetPurchaseInvoiceIdAsync(
                    tenantId,
                    payableId,
                    ct
                );
                if (purchaseInvoiceId is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PaymentDto>.NotFound(
                        $"Cuenta por pagar {payableId} no encontrada."
                    );
                }
                purchaseInvoiceIdByPayableId[payableId] = purchaseInvoiceId.Value;
            }

            foreach (
                var purchaseInvoiceId in purchaseInvoiceIdByPayableId
                    .Values.Distinct()
                    .OrderBy(id => id.ToString())
            )
            {
                await _purchaseReturnRepo.AcquireFinancialLockAsync(
                    tenantId,
                    purchaseInvoiceId,
                    ct
                );
            }

            // ── Recarga autoritativa — primera vez que se rastrea cada PurchasePayable,
            // ya dentro del lock: garantizadamente fresca. ──────────────────
            var payablesByDocId =
                new Dictionary<Guid, Domain.Modules.Purchases.Entities.PurchasePayable>();
            foreach (var line in payment.Lines)
            {
                var payableId = line.PayableId!.Value;
                if (payablesByDocId.ContainsKey(payableId))
                    continue;

                var payable = await _payables.GetByIdAsync(tenantId, payableId, ct);
                if (payable is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PaymentDto>.NotFound(
                        $"Cuenta por pagar {payableId} no encontrada."
                    );
                }
                payablesByDocId[payableId] = payable;
            }

            payment.Reverse(_u.UserId, cmd.Reason);

            foreach (var line in payment.Lines)
            {
                payablesByDocId[line.PayableId!.Value]
                    .ReversePayment(line.AppliedAmount, _u.UserId);
            }

            await _payments.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return Result<PaymentDto>.Success(Map.ToDto(payment));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
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
