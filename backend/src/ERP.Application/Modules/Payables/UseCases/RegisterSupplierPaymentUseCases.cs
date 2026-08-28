using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

// ── DTOs (Request, línea por línea) ─────────────────────────────────────

/// <summary>SUPPLIER-PAYMENTS-REGISTER-15C — un medio de pago usado en el registro.</summary>
public sealed record SupplierPaymentMethodLineRequest(
    Guid PaymentMethodId,
    Guid FinancialDestinationId,
    decimal Amount,
    string? ReferenceNumber = null,
    string? CheckNumber = null,
    DateOnly? CheckDate = null,
    string? Notes = null
);

/// <summary>SUPPLIER-PAYMENTS-REGISTER-15C — una aplicación a cuota de <c>AccountsPayableInstallment</c>.</summary>
public sealed record SupplierPaymentApplicationLineRequest(
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied
);

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — celda de la matriz medio↔cuota. Los índices referencian
/// posiciones dentro de <see cref="RegisterSupplierPaymentRequest.MethodLines"/>/
/// <see cref="RegisterSupplierPaymentRequest.ApplicationLines"/> de la misma request.
/// </summary>
public sealed record SupplierPaymentAllocationLineRequest(
    int MethodLineIndex,
    int ApplicationLineIndex,
    decimal Amount
);

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — contrato HTTP de <c>POST /api/v1/supplier-payments</c>. Nunca
/// incluye TenantId/CompanyId/BranchId — vienen del contexto autenticado, nunca del body (regla
/// global de multi-tenant). El controller lo mapea 1:1 a <see cref="RegisterSupplierPaymentCommand"/>.
/// </summary>
public sealed record RegisterSupplierPaymentRequest(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string? ReceiptNumber,
    IReadOnlyList<SupplierPaymentMethodLineRequest> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineRequest> ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineRequest> Allocations
);

// ── DTO de salida ─────────────────────────────────────────────────────────

public sealed record SupplierPaymentMethodLineDto(
    Guid Id,
    Guid PaymentMethodId,
    Guid FinancialDestinationId,
    decimal Amount,
    string? ReferenceNumber,
    string? CheckNumber,
    DateOnly? CheckDate,
    string? Notes
);

public sealed record SupplierPaymentApplicationLineDto(
    Guid Id,
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied
);

public sealed record SupplierPaymentAllocationLineDto(
    Guid Id,
    Guid SupplierPaymentMethodLineId,
    Guid SupplierPaymentApplicationLineId,
    decimal Amount
);

public sealed record SupplierPaymentDto(
    Guid Id,
    Guid SupplierId,
    Guid BranchId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string SystemNumber,
    string? ReceiptNumber,
    string DisplayNumber,
    string Status,
    IReadOnlyList<SupplierPaymentMethodLineDto> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineDto> ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineDto> Allocations,
    DateTime CreatedAt
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — registra y confirma un pago a proveedor en una sola operación
/// (sin Draft — SUPPLIER-PAYMENTS-AUDIT-15A/FOUNDATION-15B), aplicándolo contra una o más
/// <c>AccountsPayableInstallment</c>. Independiente de <c>RegisterCollectionCommand</c>
/// (Payment/PaymentApplicationLine, Collections/CxC) — no lo reutiliza ni lo toca.
/// </summary>
public sealed record RegisterSupplierPaymentCommand(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string? ReceiptNumber,
    IReadOnlyList<SupplierPaymentMethodLineRequest> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineRequest> ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineRequest> Allocations
) : IRequest<Result<SupplierPaymentDto>>, IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class SupplierPaymentMethodLineRequestValidator
    : AbstractValidator<SupplierPaymentMethodLineRequest>
{
    public SupplierPaymentMethodLineRequestValidator()
    {
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.FinancialDestinationId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class SupplierPaymentApplicationLineRequestValidator
    : AbstractValidator<SupplierPaymentApplicationLineRequest>
{
    public SupplierPaymentApplicationLineRequestValidator()
    {
        RuleFor(x => x.AccountsPayableInstallmentId).NotEmpty();
        RuleFor(x => x.AmountApplied).GreaterThan(0);
    }
}

public sealed class SupplierPaymentAllocationLineRequestValidator
    : AbstractValidator<SupplierPaymentAllocationLineRequest>
{
    public SupplierPaymentAllocationLineRequestValidator()
    {
        RuleFor(x => x.MethodLineIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ApplicationLineIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class RegisterSupplierPaymentCommandValidator
    : AbstractValidator<RegisterSupplierPaymentCommand>
{
    public RegisterSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.PaymentDate).NotEmpty();
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.MethodLines)
            .NotEmpty()
            .WithMessage("El pago debe tener al menos un medio de pago.");
        RuleFor(x => x.ApplicationLines)
            .NotEmpty()
            .WithMessage("El pago debe tener al menos una aplicación a cuota.");
        RuleFor(x => x.Allocations)
            .NotEmpty()
            .WithMessage("El pago debe tener al menos una distribución medio↔cuota.");
        RuleForEach(x => x.MethodLines).SetValidator(new SupplierPaymentMethodLineRequestValidator());
        RuleForEach(x => x.ApplicationLines)
            .SetValidator(new SupplierPaymentApplicationLineRequestValidator());
        RuleForEach(x => x.Allocations).SetValidator(new SupplierPaymentAllocationLineRequestValidator());
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class RegisterSupplierPaymentCommandHandler
    : IRequestHandler<RegisterSupplierPaymentCommand, Result<SupplierPaymentDto>>
{
    private readonly ISupplierPaymentRepository _supplierPayments;
    private readonly ISupplierPaymentSequenceRepository _sequences;
    private readonly IAccountsPayableRepository _accountsPayables;
    private readonly IPaymentMethodRepository _paymentMethods;
    private readonly ICompanyFinancialDestinationRepository _financialDestinations;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public RegisterSupplierPaymentCommandHandler(
        ISupplierPaymentRepository supplierPayments,
        ISupplierPaymentSequenceRepository sequences,
        IAccountsPayableRepository accountsPayables,
        IPaymentMethodRepository paymentMethods,
        ICompanyFinancialDestinationRepository financialDestinations,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _supplierPayments = supplierPayments;
        _sequences = sequences;
        _accountsPayables = accountsPayables;
        _paymentMethods = paymentMethods;
        _financialDestinations = financialDestinations;
        _uow = uow;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<SupplierPaymentDto>> Handle(
        RegisterSupplierPaymentCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;
        var branchId = _b.BranchId;
        var userId = _u.UserId;

        var receiptNumber = string.IsNullOrWhiteSpace(cmd.ReceiptNumber) ? null : cmd.ReceiptNumber.Trim();

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ── receipt_number único por (Tenant, Company, Supplier) si se informa ──
            if (receiptNumber is not null)
            {
                var receiptExists = await _supplierPayments.ExistsByReceiptNumberAsync(
                    tenantId,
                    companyId,
                    cmd.SupplierId,
                    receiptNumber,
                    ct
                );
                if (receiptExists)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.Conflict(
                        "Ya existe un pago con ese número de comprobante para este proveedor."
                    );
                }
            }

            // ── PaymentMethodId debe existir y estar activo ──
            foreach (var methodId in cmd.MethodLines.Select(l => l.PaymentMethodId).Distinct())
            {
                var method = await _paymentMethods.GetByIdAsync(tenantId, methodId, ct);
                if (method is null || !method.IsActive)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"El medio de pago {methodId} no existe o no está activo."
                    );
                }
            }

            // ── FinancialDestinationId debe existir, pertenecer a la empresa, estar activo y tener cuenta contable ──
            foreach (var destinationId in cmd.MethodLines.Select(l => l.FinancialDestinationId).Distinct())
            {
                var destination = await _financialDestinations.GetByIdAsync(tenantId, destinationId, ct);
                if (destination is null || destination.CompanyId != companyId)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.NotFound(
                        $"El destino financiero {destinationId} no existe o no pertenece a esta empresa."
                    );
                }
                if (!destination.IsActive)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"El destino financiero {destinationId} no está activo."
                    );
                }
                if (destination.AccountingAccountId == Guid.Empty)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"El destino financiero {destinationId} no tiene una cuenta contable configurada."
                    );
                }
            }

            // ── Carga y valida cada cuota referenciada, agrupando por AccountsPayable dueño ──
            var payablesByInstallment = new Dictionary<Guid, AccountsPayable>();
            foreach (var appLine in cmd.ApplicationLines)
            {
                var installmentId = appLine.AccountsPayableInstallmentId;
                if (!payablesByInstallment.ContainsKey(installmentId))
                {
                    var payable = await _accountsPayables.GetByInstallmentIdAsync(tenantId, installmentId, ct);
                    if (payable is null)
                    {
                        await _uow.RollbackAsync(ct);
                        return Result<SupplierPaymentDto>.NotFound(
                            $"La cuota {installmentId} no existe."
                        );
                    }
                    if (payable.SupplierId != cmd.SupplierId)
                    {
                        await _uow.RollbackAsync(ct);
                        return Result<SupplierPaymentDto>.ValidationFailure(
                            "No se pueden mezclar cuotas de distintos proveedores en un mismo pago."
                        );
                    }
                    if (payable.CompanyId != companyId)
                    {
                        await _uow.RollbackAsync(ct);
                        return Result<SupplierPaymentDto>.ValidationFailure(
                            "La cuota indicada no pertenece a esta empresa."
                        );
                    }

                    payablesByInstallment[installmentId] = payable;
                }

                var installment = payablesByInstallment[installmentId]
                    .Installments.First(i => i.Id == installmentId);

                if (
                    installment.Status is AccountsPayableStatus.Cancelled or AccountsPayableStatus.Paid
                )
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La cuota {installmentId} está {installment.Status} y no admite pagos."
                    );
                }
                if (installment.OutstandingAmount <= 0)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La cuota {installmentId} no tiene saldo pendiente."
                    );
                }
                if (appLine.AmountApplied > installment.OutstandingAmount)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"El monto aplicado a la cuota {installmentId} excede su saldo pendiente."
                    );
                }
            }

            // ── system_number ──
            string systemNumber;
            try
            {
                systemNumber = await _sequences.CaptureNextAsync(tenantId, companyId, ct);
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }

            // ── Construye y confirma el agregado (invariantes de balance/distribución en dominio) ──
            SupplierPayment payment;
            try
            {
                payment = SupplierPayment.Create(
                    tenantId,
                    companyId,
                    branchId,
                    cmd.SupplierId,
                    cmd.PaymentDate,
                    cmd.TotalAmount,
                    systemNumber,
                    receiptNumber,
                    cmd.MethodLines
                        .Select(l => new SupplierPaymentMethodLineInput(
                            l.PaymentMethodId,
                            l.FinancialDestinationId,
                            l.Amount,
                            l.ReferenceNumber,
                            l.CheckNumber,
                            l.CheckDate,
                            l.Notes
                        ))
                        .ToList(),
                    cmd.ApplicationLines
                        .Select(l => new SupplierPaymentApplicationLineInput(
                            l.AccountsPayableInstallmentId,
                            l.AmountApplied
                        ))
                        .ToList(),
                    cmd.Allocations
                        .Select(a => new SupplierPaymentAllocationInput(
                            a.MethodLineIndex,
                            a.ApplicationLineIndex,
                            a.Amount
                        ))
                        .ToList(),
                    userId
                );
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
            }

            // ── Aplica cada monto a su cuota puntual y recalcula AccountsPayable cabecera ──
            foreach (var appLine in cmd.ApplicationLines)
            {
                try
                {
                    payablesByInstallment[appLine.AccountsPayableInstallmentId]
                        .RegisterPaymentToInstallment(
                            appLine.AccountsPayableInstallmentId,
                            appLine.AmountApplied,
                            userId
                        );
                }
                catch (InvalidOperationException ex)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
                }
            }

            await _supplierPayments.AddAsync(payment, ct);

            try
            {
                // SUPPLIER-PAYMENTS-POSTING-15D: SaveChangesAsync publica SupplierPaymentConfirmedEvent
                // ANTES del commit (ErpDbContext.SaveChangesAsync, ADR-026 §8) —
                // SupplierPaymentConfirmedPostingTranslator lanza SupplierPaymentPostingFailedException
                // (nunca solo un warning) si el asiento no puede generarse. "No confirmar pago sin
                // asiento": el catch de abajo revierte la transacción completa — ni el SupplierPayment
                // ni los saldos de AccountsPayableInstallment mutados arriba llegan a persistirse.
                await _supplierPayments.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(
                    "Una de las cuentas por pagar afectadas fue modificada concurrentemente. Intente nuevamente."
                );
            }
            catch (SupplierPaymentPostingFailedException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(ex.Message, ex.Code);
            }

            await _uow.CommitAsync(ct);
            return Result<SupplierPaymentDto>.Success(Map.ToDto(payment), ApiResponseCodes.Common.Created);
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
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
    public static SupplierPaymentDto ToDto(SupplierPayment p) =>
        new(
            p.Id,
            p.SupplierId,
            p.BranchId,
            p.PaymentDate,
            p.TotalAmount,
            p.SystemNumber,
            p.ReceiptNumber,
            p.DisplayNumber,
            p.Status.ToString(),
            p.MethodLines
                .Select(l => new SupplierPaymentMethodLineDto(
                    l.Id,
                    l.PaymentMethodId,
                    l.FinancialDestinationId,
                    l.Amount,
                    l.ReferenceNumber,
                    l.CheckNumber,
                    l.CheckDate,
                    l.Notes
                ))
                .ToList(),
            p.ApplicationLines
                .Select(l => new SupplierPaymentApplicationLineDto(
                    l.Id,
                    l.AccountsPayableInstallmentId,
                    l.AmountApplied
                ))
                .ToList(),
            p.AllocationLines
                .Select(l => new SupplierPaymentAllocationLineDto(
                    l.Id,
                    l.SupplierPaymentMethodLineId,
                    l.SupplierPaymentApplicationLineId,
                    l.Amount
                ))
                .ToList(),
            p.CreatedAt
        );
}
