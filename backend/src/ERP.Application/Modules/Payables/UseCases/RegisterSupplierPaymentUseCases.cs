using ERP.Application.Common;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

// ── DTOs (Request, línea por línea) ─────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — un medio de pago usado en el registro.
/// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — <see cref="TransactionDate"/>: fecha efectiva
/// real de una fuente bancaria — obligatoria y explícita (02A-FINAL: nunca completada con
/// PaymentDate); prohibida en fuentes de caja. <see cref="ReferenceNumber"/> es el único número de operación bancaria.
/// </summary>
public sealed record SupplierPaymentMethodLineRequest(
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    decimal Amount,
    string? ReferenceNumber = null,
    string? CheckNumber = null,
    DateOnly? CheckDate = null,
    string? Notes = null,
    DateOnly? TransactionDate = null
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
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    decimal Amount,
    string? ReferenceNumber,
    string? CheckNumber,
    DateOnly? CheckDate,
    string? Notes,
    DateOnly? TransactionDate = null,
    Guid? CashSessionId = null,
    Guid? CashMovementId = null
);

/// <summary>
/// SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — <c>SupplierPaymentApplicationLine</c>
/// (dominio) solo guarda <see cref="AccountsPayableInstallmentId"/>/<see cref="AmountApplied"/>, sin
/// snapshot de documento — los campos de solo lectura de abajo (<see cref="DocumentNumber"/>,
/// <see cref="InstallmentNumber"/>, <see cref="DueDate"/>, <see cref="IssueDate"/>,
/// <see cref="OriginType"/>) se resuelven en el momento de la consulta contra
/// <see cref="ERP.Domain.Modules.Payables.Entities.AccountsPayable"/>/
/// <c>AccountsPayableInstallment</c> (mismo dato que ya expone <c>PayablesController</c>, nunca un
/// duplicado). Quedan <c>null</c> — nunca rompen el detalle — si la cuota ya no puede resolverse
/// (caso excepcional); el frontend cae a un fallback técnico con el Id crudo en ese caso.
/// </summary>
public sealed record SupplierPaymentApplicationLineDto(
    Guid Id,
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied,
    string? DocumentNumber = null,
    int? InstallmentNumber = null,
    DateOnly? DueDate = null,
    DateOnly? IssueDate = null,
    string? OriginType = null
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
    DateTime CreatedAt,
    DateTime? ReversedAtUtc = null,
    Guid? ReversedBy = null,
    string? ReverseReason = null
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — registra y confirma un pago a proveedor en una sola operación
/// (sin Draft — SUPPLIER-PAYMENTS-AUDIT-15A/FOUNDATION-15B), aplicándolo contra una o más
/// <c>AccountsPayableInstallment</c>. Independiente de <c>RegisterCollectionCommand</c>
/// (Payment/PaymentApplicationLine, Collections/CxC) — no lo reutiliza ni lo toca.
/// PAYABLES-BRANCH-SCOPE-DECISION-01 — a diferencia de las queries de lectura de este módulo (ver
/// <c>GetSupplierPaymentByIdQuery</c>), este Command SÍ se mantiene <c>IBranchScopedRequest</c>
/// deliberadamente: <c>SupplierPayment.Create()</c> exige <c>branchId != Guid.Empty</c> como
/// invariante de dominio (registra desde qué sucursal se emitió el pago, por trazabilidad — no como
/// filtro de acceso), así que el handler necesita una sucursal activa válida para poder construir el
/// agregado, aunque la autorización real (destino financiero, cuota, CxP) siga siendo 100%
/// company-level. No mueve caja/banco por sucursal — ningún dato se filtra ni se restringe por
/// <c>BranchId</c>.
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
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.CompanyBankAccountId is not null ^ x.CashRegisterId is not null)
            .WithMessage("Debe especificar exactamente una cuenta bancaria o una caja destino.");
        RuleFor(x => x.TransactionDate)
            .Null()
            .When(x => x.CashRegisterId is not null)
            .WithMessage("La fecha de transacción bancaria no aplica a un medio de pago en caja.");
        // 02A-FINAL — explícita, nunca completada con PaymentDate en backend.
        RuleFor(x => x.TransactionDate)
            .NotNull()
            .When(x => x.CompanyBankAccountId is not null)
            .WithMessage("La fecha de la transacción bancaria es obligatoria.");
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
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly ICashSessionRepository _cashSessions;
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
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        ICashSessionRepository cashSessions,
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
        _bankAccounts = bankAccounts;
        _cashRegisters = cashRegisters;
        _cashSessions = cashSessions;
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
            var methodsById = new Dictionary<Guid, PaymentMethod>();
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
                methodsById[methodId] = method;
            }

            // ── 02A: medio ↔ destino, PaymentMethod como SSOT (fail-closed) ──
            foreach (var line in cmd.MethodLines)
            {
                var lineError = ValidateMethodLineAgainstCatalog(line, methodsById[line.PaymentMethodId]);
                if (lineError is not null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(lineError);
                }
            }

            // ── Cuenta bancaria/caja debe existir, pertenecer a la empresa, estar activa y tener cuenta contable ──
            foreach (var bankAccountId in cmd.MethodLines
                .Where(l => l.CompanyBankAccountId is not null)
                .Select(l => l.CompanyBankAccountId!.Value)
                .Distinct())
            {
                var bankAccount = await _bankAccounts.GetByIdAsync(tenantId, bankAccountId, ct);
                if (bankAccount is null || bankAccount.CompanyId != companyId)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.NotFound(
                        $"La cuenta bancaria {bankAccountId} no existe o no pertenece a esta empresa."
                    );
                }
                if (!bankAccount.IsActive)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La cuenta bancaria {bankAccountId} no está activa."
                    );
                }
            }
            foreach (var cashRegisterId in cmd.MethodLines
                .Where(l => l.CashRegisterId is not null)
                .Select(l => l.CashRegisterId!.Value)
                .Distinct())
            {
                var cashRegister = await _cashRegisters.GetByIdAsync(tenantId, cashRegisterId, ct);
                if (cashRegister is null || cashRegister.CompanyId != companyId)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.NotFound(
                        $"La caja {cashRegisterId} no existe o no pertenece a esta empresa."
                    );
                }
                if (!cashRegister.IsActive)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La caja {cashRegisterId} no está activa."
                    );
                }
                if (cashRegister.AccountingAccountId is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La caja {cashRegisterId} no tiene una cuenta contable configurada."
                    );
                }
            }

            // ── 02A: cada caja exige su CashSession Open — el egreso operativo se registra en esa
            // sesión. 02A-FINAL: lock exclusivo (FOR UPDATE) ANTES de leer el saldo, en orden
            // determinista por CashRegisterId: dos pagos concurrentes sobre la misma caja quedan
            // serializados (el segundo ve el saldo ya consumido y recibe la validación normal) y
            // dos pagos con varias cajas nunca se bloquean en cruz ──
            var openSessionsByRegister = new Dictionary<Guid, CashSession>();
            foreach (var cashRegisterId in cmd.MethodLines
                .Where(l => l.CashRegisterId is not null)
                .Select(l => l.CashRegisterId!.Value)
                .Distinct()
                .OrderBy(id => id))
            {
                var session = await _cashSessions.GetOpenByCashRegisterForUpdateAsync(tenantId, cashRegisterId, ct);
                if (session is null || session.CompanyId != companyId)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"No existe una sesión de caja abierta para la caja {cashRegisterId}. Abra la caja antes de pagar en efectivo."
                    );
                }
                openSessionsByRegister[cashRegisterId] = session;
            }

            // ── 02A-CLOSE: sin sobregiro de caja (fail-closed, sin override). El consumo se ACUMULA
            // por sesión: varias líneas de efectivo del mismo pago contra la misma caja nunca pueden
            // superar juntas el efectivo esperado (CashSession.CurrentBalance, SSOT del arqueo) ──
            foreach (var cashGroup in cmd.MethodLines
                .Where(l => l.CashRegisterId is not null)
                .GroupBy(l => l.CashRegisterId!.Value))
            {
                var requested = cashGroup.Sum(l => l.Amount);
                var available = openSessionsByRegister[cashGroup.Key].CurrentBalance;
                if (requested > available)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La caja seleccionada dispone de ${FormatMoney(available)} y se intenta registrar un pago de ${FormatMoney(requested)}."
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
                            l.CompanyBankAccountId,
                            l.CashRegisterId,
                            l.Amount,
                            l.ReferenceNumber,
                            l.CheckNumber,
                            l.CheckDate,
                            l.Notes,
                            l.TransactionDate
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

            // ── 02A: efecto operativo de caja — un egreso por cada fuente de caja, vinculado a la
            // línea. CashMovement nunca postea: el asiento sigue siendo solo de SupplierPayment ──
            foreach (var methodLine in payment.MethodLines.Where(l => l.CashRegisterId is not null))
            {
                var session = openSessionsByRegister[methodLine.CashRegisterId!.Value];
                try
                {
                    var movement = session.RecordMovement(
                        CashMovementType.SupplierPayment,
                        methodLine.Amount,
                        $"Pago a proveedor {payment.SystemNumber}",
                        userId,
                        CashReferenceType.SupplierPayment,
                        payment.Id,
                        payment.SystemNumber
                    );
                    payment.LinkCashMovement(methodLine.Id, session.Id, movement.Id);
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
            return Result<SupplierPaymentDto>.Success(
                SupplierPaymentDtoMapper.ToDto(payment),
                ApiResponseCodes.Common.Created
            );
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

    /// <summary>Montos en mensajes: punto decimal y 2 decimales, siempre InvariantCulture (estándar de decimales).</summary>
    private static string FormatMoney(decimal amount) =>
        amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — reglas medio ↔ destino con
    /// <see cref="PaymentMethod"/> como SSOT: medio de crédito prohibido; efectivo físico ⇒ caja
    /// (banco prohibido); cualquier otro medio ⇒ cuenta bancaria (caja prohibida); medio con
    /// <see cref="PaymentMethod.RequiresReference"/> ⇒ número de operación (o de cheque) obligatorio.
    /// </summary>
    internal static string? ValidateMethodLineAgainstCatalog(
        SupplierPaymentMethodLineRequest line,
        PaymentMethod method
    )
    {
        if (method.IsCreditAllowed)
            return $"El medio de pago {method.Name} es de crédito y no puede usarse para pagar a un proveedor.";

        if (method.AffectsPhysicalCash)
        {
            if (line.CompanyBankAccountId is not null || line.CashRegisterId is null)
                return $"El medio de pago {method.Name} mueve efectivo físico: el destino debe ser una caja, no una cuenta bancaria.";
            return null;
        }

        if (line.CashRegisterId is not null || line.CompanyBankAccountId is null)
            return $"El medio de pago {method.Name} es bancario: el destino debe ser una cuenta bancaria, no una caja.";

        // 02A-FINAL — fecha real del extracto, explícita; nunca se completa con PaymentDate.
        if (line.TransactionDate is null)
            return "La fecha de la transacción bancaria es obligatoria.";

        if (method.RequiresReference)
        {
            var isCheck = method.DetailType == PaymentMethodDetailType.Check;
            var reference = isCheck ? line.CheckNumber : line.ReferenceNumber;
            if (string.IsNullOrWhiteSpace(reference))
                return isCheck
                    ? $"El medio de pago {method.Name} exige el número de cheque."
                    : $"El medio de pago {method.Name} exige el número de operación bancaria (referencia).";
        }

        return null;
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

// SUPPLIER-PAYMENTS-FRONTEND-15E: internal (no longer file-scoped) para que
// GetSupplierPaymentUseCases.cs (misma capa, mismo namespace) reutilice el mismo mapeo — sin
// duplicar la fuente de verdad de "cómo se ve un SupplierPaymentDto".
/// <summary>
/// SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — datos de solo lectura de la cuota
/// (documento origen + cuota), resueltos contra <c>AccountsPayable</c>/<c>AccountsPayableInstallment</c>
/// para proyectar <see cref="SupplierPaymentApplicationLineDto"/> sin exponer el GUID crudo.
/// </summary>
internal sealed record InstallmentDisplayInfo(
    string DocumentNumber,
    int InstallmentNumber,
    DateOnly DueDate,
    DateOnly IssueDate,
    string OriginType
);

internal static class SupplierPaymentDtoMapper
{
    /// <summary>
    /// <paramref name="installmentDisplayInfo"/> es opcional (ausente para las respuestas de
    /// Register/Reverse, que no lo necesitan porque el frontend navega de inmediato al detalle,
    /// que sí lo resuelve) — sin él, <see cref="SupplierPaymentApplicationLineDto"/> simplemente
    /// deja sus campos de proyección en <c>null</c> (fallback técnico en el frontend).
    /// </summary>
    public static SupplierPaymentDto ToDto(
        SupplierPayment p,
        IReadOnlyDictionary<Guid, InstallmentDisplayInfo>? installmentDisplayInfo = null
    ) =>
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
                    l.CompanyBankAccountId,
                    l.CashRegisterId,
                    l.Amount,
                    l.ReferenceNumber,
                    l.CheckNumber,
                    l.CheckDate,
                    l.Notes,
                    l.TransactionDate,
                    l.CashSessionId,
                    l.CashMovementId
                ))
                .ToList(),
            p.ApplicationLines
                .Select(l =>
                {
                    InstallmentDisplayInfo? info = null;
                    installmentDisplayInfo?.TryGetValue(l.AccountsPayableInstallmentId, out info);
                    return new SupplierPaymentApplicationLineDto(
                        l.Id,
                        l.AccountsPayableInstallmentId,
                        l.AmountApplied,
                        info?.DocumentNumber,
                        info?.InstallmentNumber,
                        info?.DueDate,
                        info?.IssueDate,
                        info?.OriginType
                    );
                })
                .ToList(),
            p.AllocationLines
                .Select(l => new SupplierPaymentAllocationLineDto(
                    l.Id,
                    l.SupplierPaymentMethodLineId,
                    l.SupplierPaymentApplicationLineId,
                    l.Amount
                ))
                .ToList(),
            p.CreatedAt,
            p.ReversedAtUtc,
            p.ReversedBy,
            p.ReverseReason
        );
}
