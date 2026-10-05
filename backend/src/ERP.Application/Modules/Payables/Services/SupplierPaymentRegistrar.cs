using ERP.Application.Common;
using ERP.Application.Modules.Caja;
using ERP.Application.Modules.Finance;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Common;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Payables.Services;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — contexto explícito de un registro de pago: empresa,
/// sucursal y los DOS actores sin falsificar <c>ICurrentUser</c>.
/// <list type="bullet">
/// <item><see cref="OriginatorUserId"/> — quien preparó el pago (<c>SupplierPayment.CreatedBy</c>,
/// <c>SupplierCredit.CreatedBy</c> del anticipo).</item>
/// <item><see cref="ExecutorUserId"/> — quien lo ejecuta: debe controlar cada <c>CashSession</c>
/// usada (ownership), registra el <c>CashMovement</c> y la mutación de la CxP, y queda como
/// <c>SupplierPayment.ConfirmedByUserId</c>.</item>
/// </list>
/// En el pago directo ambos son el usuario actual.
/// </summary>
public sealed record SupplierPaymentRegistrationContext(
    Guid TenantId,
    Guid CompanyId,
    Guid BranchId,
    Guid OriginatorUserId,
    Guid ExecutorUserId
);

/// <summary>Resultado del núcleo: el agregado ya persistido (sin commit) y el anticipo originado, si hubo.</summary>
public sealed record SupplierPaymentRegistration(SupplierPayment Payment, Guid? SupplierCreditId);

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — núcleo ÚNICO de registro de un pago a proveedor,
/// extraído de <c>RegisterSupplierPaymentCommandHandler</c> sin cambios de reglas, para ser usado por
/// (A) el pago directo y (B) la ejecución de una solicitud de efectivo.
/// <para>
/// Contrato transaccional: se ejecuta DENTRO de una transacción ya abierta por el llamador y nunca la
/// abre, confirma ni revierte. Un <c>Result</c> fallido (o una excepción) obliga al llamador a
/// revertir. En éxito deja el pago guardado (<c>SaveChanges</c>, que dispara el posting fail-closed)
/// pendiente del commit del llamador.
/// </para>
/// <para>
/// Locks: toma <c>CashSession</c> FOR UPDATE por <c>CashRegisterId</c> en orden determinista (patrón
/// oficial). Un llamador que ya bloqueó la sesión (orden único CashSession → CashFundingRequest →
/// resto) re-adquiere el mismo lock en su propia transacción sin conflicto.
/// </para>
/// </summary>
public interface ISupplierPaymentRegistrar
{
    /// <summary>
    /// Reglas del pago que no requieren locks ni efectos (remanente sin confirmar, pago sin CxP no
    /// permitido por la empresa). Devuelve el mensaje de rechazo o <c>null</c>. El pago directo la
    /// evalúa ANTES de abrir su transacción (rechazo sin ningún efecto, 02C); <see cref="RegisterAsync"/>
    /// la re-evalúa siempre, así ningún llamador puede saltársela.
    /// </summary>
    Task<string?> PrevalidateAsync(RegisterSupplierPaymentCommand intent, CancellationToken ct);

    /// <summary>
    /// ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — TODAS las validaciones del pago (las mismas de
    /// <see cref="RegisterAsync"/>: intención, comprobante, medios, destinos, caja FOR UPDATE +
    /// ownership del ejecutor + sucursal + saldo, CxP) sin ningún efecto: ni secuencia, ni pago, ni
    /// caja, ni asiento. Debe correr dentro de la transacción del llamador (toma el lock de caja).
    /// Usado para validar una solicitud de efectivo "como si el cajero la ejecutara ahora".
    /// </summary>
    Task<Result<bool>> ValidateAsync(
        RegisterSupplierPaymentCommand intent,
        SupplierPaymentRegistrationContext context,
        CancellationToken ct
    );

    /// <param name="intent"></param>

    /// <param name="context"></param>
    /// <param name="ct"></param>    /// <param name="clientRequest">
    /// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — intención del cliente del pago directo: se vincula al
    /// pago antes del INSERT, así el índice único (TenantId, ClientRequestId) protege la misma
    /// transacción que aplica CxP, mueve caja, crea el anticipo y postea. Null en la ejecución de una
    /// solicitud de efectivo (idempotente por el estado de la solicitud).
    /// </param>
    Task<Result<SupplierPaymentRegistration>> RegisterAsync(
        RegisterSupplierPaymentCommand intent,
        SupplierPaymentRegistrationContext context,
        CancellationToken ct,
        ClientRequestKey? clientRequest = null
    );
}

public sealed class SupplierPaymentRegistrar : ISupplierPaymentRegistrar
{
    private readonly ISupplierPaymentRepository _supplierPayments;
    private readonly ISupplierPaymentSequenceRepository _sequences;
    private readonly IAccountsPayableRepository _accountsPayables;
    private readonly IPaymentMethodRepository _paymentMethods;
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly ICashSessionRepository _cashSessions;
    private readonly ISupplierCreditRepository _supplierCredits;
    private readonly IOperationalPreferencesResolver _preferences;
    private readonly ICompanyRepository _companies;

    public SupplierPaymentRegistrar(
        ISupplierPaymentRepository supplierPayments,
        ISupplierPaymentSequenceRepository sequences,
        IAccountsPayableRepository accountsPayables,
        IPaymentMethodRepository paymentMethods,
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        ICashSessionRepository cashSessions,
        ISupplierCreditRepository supplierCredits,
        IOperationalPreferencesResolver preferences,
        ICompanyRepository companies
    )
    {
        _supplierPayments = supplierPayments;
        _sequences = sequences;
        _accountsPayables = accountsPayables;
        _paymentMethods = paymentMethods;
        _bankAccounts = bankAccounts;
        _cashRegisters = cashRegisters;
        _cashSessions = cashSessions;
        _supplierCredits = supplierCredits;
        _preferences = preferences;
        _companies = companies;
    }

    public async Task<Result<bool>> ValidateAsync(
        RegisterSupplierPaymentCommand intent,
        SupplierPaymentRegistrationContext context,
        CancellationToken ct
    )
    {
        var prepared = await PrepareAsync(intent, context, ct);
        return prepared.IsSuccess
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(prepared.Error!, prepared.Code);
    }

    /// <summary>Validación completa + locks, sin efectos: devuelve lo ya cargado para ejecutar.</summary>
    private async Task<Result<PreparedPayment>> PrepareAsync(
        RegisterSupplierPaymentCommand cmd,
        SupplierPaymentRegistrationContext context,
        CancellationToken ct
    )
    {
        var tenantId = context.TenantId;
        var companyId = context.CompanyId;
        var branchId = context.BranchId;
        var executorId = context.ExecutorUserId;

        var receiptNumber = string.IsNullOrWhiteSpace(cmd.ReceiptNumber)
            ? null
            : cmd.ReceiptNumber.Trim();

        var (intentError, allowWithoutPayable) = await CheckIntentAsync(cmd, ct);
        if (intentError is not null)
            return Result<PreparedPayment>.ValidationFailure(intentError);

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
                return Result<PreparedPayment>.Conflict(
                    "Ya existe un pago con ese número de comprobante para este proveedor."
                );
        }

        // ── PaymentMethodId debe existir y estar activo ──
        var methodsById = new Dictionary<Guid, PaymentMethod>();
        foreach (var methodId in cmd.MethodLines.Select(l => l.PaymentMethodId).Distinct())
        {
            var method = await _paymentMethods.GetByIdAsync(tenantId, methodId, ct);
            if (method is null || !method.IsActive)
                return Result<PreparedPayment>.ValidationFailure(
                    $"El medio de pago {methodId} no existe o no está activo."
                );
            methodsById[methodId] = method;
        }

        // ── 02A: medio ↔ destino, PaymentMethod como SSOT (fail-closed) ──
        foreach (var line in cmd.MethodLines)
        {
            var lineError = ValidateMethodLineAgainstCatalog(
                line,
                methodsById[line.PaymentMethodId]
            );
            if (lineError is not null)
                return Result<PreparedPayment>.ValidationFailure(lineError);
        }

        // ── Cuenta bancaria/caja debe existir, pertenecer a la empresa, estar activa y tener cuenta contable ──
        foreach (
            var bankAccountId in cmd
                .MethodLines.Where(l => l.CompanyBankAccountId is not null)
                .Select(l => l.CompanyBankAccountId!.Value)
                .Distinct()
        )
        {
            var bankAccount = await _bankAccounts.GetByIdAsync(tenantId, bankAccountId, ct);
            if (bankAccount is null || bankAccount.CompanyId != companyId)
                return Result<PreparedPayment>.NotFound(
                    $"La cuenta bancaria {bankAccountId} no existe o no pertenece a esta empresa."
                );
            if (!bankAccount.IsActive)
                return Result<PreparedPayment>.ValidationFailure(
                    $"La cuenta bancaria {bankAccountId} no está activa."
                );
        }
        foreach (
            var cashRegisterId in cmd
                .MethodLines.Where(l => l.CashRegisterId is not null)
                .Select(l => l.CashRegisterId!.Value)
                .Distinct()
        )
        {
            var cashRegister = await _cashRegisters.GetByIdAsync(tenantId, cashRegisterId, ct);
            if (cashRegister is null || cashRegister.CompanyId != companyId)
                return Result<PreparedPayment>.NotFound(
                    $"La caja {cashRegisterId} no existe o no pertenece a esta empresa."
                );
            if (!cashRegister.IsActive)
                return Result<PreparedPayment>.ValidationFailure(
                    $"La caja {cashRegisterId} no está activa."
                );
            if (cashRegister.AccountingAccountId is null)
                return Result<PreparedPayment>.ValidationFailure(
                    $"La caja {cashRegisterId} no tiene una cuenta contable configurada."
                );
        }

        // ── 02A: cada caja exige su CashSession Open — el egreso operativo se registra en esa
        // sesión. 02A-FINAL: lock exclusivo (FOR UPDATE) ANTES de leer el saldo, en orden
        // determinista por CashRegisterId: dos pagos concurrentes sobre la misma caja quedan
        // serializados (el segundo ve el saldo ya consumido y recibe la validación normal) y
        // dos pagos con varias cajas nunca se bloquean en cruz ──
        var openSessionsByRegister = new Dictionary<Guid, CashSession>();
        foreach (
            var cashRegisterId in cmd
                .MethodLines.Where(l => l.CashRegisterId is not null)
                .Select(l => l.CashRegisterId!.Value)
                .Distinct()
                .OrderBy(id => id)
        )
        {
            var session = await _cashSessions.GetOpenByCashRegisterForUpdateAsync(
                tenantId,
                cashRegisterId,
                ct
            );
            if (session is null || session.CompanyId != companyId)
                return Result<PreparedPayment>.ValidationFailure(
                    $"No existe una sesión de caja abierta para la caja {cashRegisterId}. Abra la caja antes de pagar en efectivo."
                );
            // 02B — autoridad sobre la sesión: solo quien la opera (CashSession.UserId) puede sacar
            // efectivo de ella, y solo desde la sucursal activa. Sin bypass por rol. 02E-B: la
            // autoridad es del EJECUTOR (en el pago directo, el usuario actual; al atender una
            // solicitud de efectivo, el cajero que controla la sesión) — nunca se exceptúa.
            if (!session.IsControlledBy(executorId))
                return Result<PreparedPayment>.ValidationFailure(
                    CashSessionOwnership.RejectionMessage(session)
                );
            if (session.BranchId != branchId)
                return Result<PreparedPayment>.ValidationFailure(
                    "La caja seleccionada no pertenece a la sucursal activa."
                );
            openSessionsByRegister[cashRegisterId] = session;
        }

        // ── 02A-CLOSE: sin sobregiro de caja (fail-closed, sin override). El consumo se ACUMULA
        // por sesión: varias líneas de efectivo del mismo pago contra la misma caja nunca pueden
        // superar juntas el efectivo esperado (CashSession.CurrentBalance, SSOT del arqueo) ──
        foreach (
            var cashGroup in cmd
                .MethodLines.Where(l => l.CashRegisterId is not null)
                .GroupBy(l => l.CashRegisterId!.Value)
        )
        {
            var requested = cashGroup.Sum(l => l.Amount);
            var available = openSessionsByRegister[cashGroup.Key].CurrentBalance;
            if (requested > available)
                return Result<PreparedPayment>.ValidationFailure(
                    $"La caja seleccionada dispone de ${FormatMoney(available)} y se intenta registrar un pago de ${FormatMoney(requested)}."
                );
        }

        // ── Carga y valida cada cuota referenciada, agrupando por AccountsPayable dueño ──
        var payablesByInstallment = new Dictionary<Guid, AccountsPayable>();
        foreach (var appLine in cmd.ApplicationLines)
        {
            var installmentId = appLine.AccountsPayableInstallmentId;
            if (!payablesByInstallment.ContainsKey(installmentId))
            {
                var payable = await _accountsPayables.GetByInstallmentIdAsync(
                    tenantId,
                    installmentId,
                    ct
                );
                if (payable is null)
                    return Result<PreparedPayment>.NotFound($"La cuota {installmentId} no existe.");
                if (payable.SupplierId != cmd.SupplierId)
                    return Result<PreparedPayment>.ValidationFailure(
                        "No se pueden mezclar cuotas de distintos proveedores en un mismo pago."
                    );
                if (payable.CompanyId != companyId)
                    return Result<PreparedPayment>.ValidationFailure(
                        "La cuota indicada no pertenece a esta empresa."
                    );

                payablesByInstallment[installmentId] = payable;
            }

            var installment = payablesByInstallment[installmentId]
                .Installments.First(i => i.Id == installmentId);

            if (installment.Status is AccountsPayableStatus.Cancelled or AccountsPayableStatus.Paid)
                return Result<PreparedPayment>.ValidationFailure(
                    $"La cuota {installmentId} está {installment.Status} y no admite pagos."
                );
            if (installment.OutstandingAmount <= 0)
                return Result<PreparedPayment>.ValidationFailure(
                    $"La cuota {installmentId} no tiene saldo pendiente."
                );
            if (appLine.AmountApplied > installment.OutstandingAmount)
                return Result<PreparedPayment>.ValidationFailure(
                    $"El monto aplicado a la cuota {installmentId} excede su saldo pendiente."
                );
        }

        return Result<PreparedPayment>.Success(
            new PreparedPayment(
                receiptNumber,
                allowWithoutPayable,
                openSessionsByRegister,
                payablesByInstallment
            )
        );
    }

    private sealed record PreparedPayment(
        string? ReceiptNumber,
        bool AllowWithoutPayable,
        Dictionary<Guid, CashSession> OpenSessionsByRegister,
        Dictionary<Guid, AccountsPayable> PayablesByInstallment
    );

    public async Task<Result<SupplierPaymentRegistration>> RegisterAsync(
        RegisterSupplierPaymentCommand cmd,
        SupplierPaymentRegistrationContext context,
        CancellationToken ct,
        ClientRequestKey? clientRequest = null
    )
    {
        var prepared = await PrepareAsync(cmd, context, ct);
        if (!prepared.IsSuccess)
            return Result<SupplierPaymentRegistration>.Failure(prepared.Error!, prepared.Code);

        var tenantId = context.TenantId;
        var companyId = context.CompanyId;
        var branchId = context.BranchId;
        var originatorId = context.OriginatorUserId;
        var executorId = context.ExecutorUserId;
        var (receiptNumber, allowWithoutPayable, openSessionsByRegister, payablesByInstallment) =
            prepared.Value!;

        // ── system_number ──
        var systemNumber = await _sequences.CaptureNextAsync(tenantId, companyId, ct);

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
                cmd.MethodLines.Select(l => new SupplierPaymentMethodLineInput(
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
                cmd.ApplicationLines.Select(l => new SupplierPaymentApplicationLineInput(
                        l.AccountsPayableInstallmentId,
                        l.AmountApplied
                    ))
                    .ToList(),
                cmd.Allocations.Select(a => new SupplierPaymentAllocationInput(
                        a.MethodLineIndex,
                        a.ApplicationLineIndex,
                        a.Amount
                    ))
                    .ToList(),
                originatorId,
                cmd.ConfirmUnappliedAmount,
                allowWithoutPayable,
                confirmedBy: executorId
            );
        }
        catch (DomainRuleViolationException ex)
        {
            return Result<SupplierPaymentRegistration>.FromDomainRule(ex);
        }
        catch (ArgumentException ex)
        {
            return Result<SupplierPaymentRegistration>.ValidationFailure(ex.Message);
        }

        if (clientRequest is { } requestKey)
            payment.BindClientRequest(requestKey);

        // ── Aplica cada monto a su cuota puntual y recalcula AccountsPayable cabecera ──
        foreach (var appLine in cmd.ApplicationLines)
        {
            try
            {
                payablesByInstallment[appLine.AccountsPayableInstallmentId]
                    .RegisterPaymentToInstallment(
                        appLine.AccountsPayableInstallmentId,
                        appLine.AmountApplied,
                        executorId
                    );
            }
            catch (DomainRuleViolationException ex)
            {
                return Result<SupplierPaymentRegistration>.FromDomainRule(ex);
            }
        }

        // ── 02A: efecto operativo de caja — un egreso por cada fuente de caja, vinculado a la
        // línea. CashMovement nunca postea: el asiento sigue siendo solo de SupplierPayment. Lo
        // registra el ejecutor (quien controla la sesión y entrega el efectivo) ──
        foreach (var methodLine in payment.MethodLines.Where(l => l.CashRegisterId is not null))
        {
            var session = openSessionsByRegister[methodLine.CashRegisterId!.Value];
            try
            {
                var movement = session.RecordMovement(
                    CashMovementType.SupplierPayment,
                    methodLine.Amount,
                    $"Pago a proveedor {payment.SystemNumber}",
                    executorId,
                    CashReferenceType.SupplierPayment,
                    payment.Id,
                    payment.SystemNumber
                );
                payment.LinkCashMovement(methodLine.Id, session.Id, movement.Id);
            }
            catch (DomainRuleViolationException ex)
            {
                return Result<SupplierPaymentRegistration>.FromDomainRule(ex);
            }
        }

        // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — el remanente no aplicado vive SOLO en
        // SupplierCredit (SSOT del anticipo), por exactamente UnappliedAmount, misma transacción.
        // SupplierCredit NO contabiliza al crearse: el Debe "Anticipos a proveedores" lo genera el
        // asiento del propio SupplierPayment (único posting financiero).
        SupplierCredit? advance = null;
        if (payment.UnappliedAmount > 0)
        {
            var company = await _companies.GetByIdAsync(companyId, ct);
            if (company is null)
                return Result<SupplierPaymentRegistration>.NotFound("Empresa no encontrada.");
            advance = SupplierCredit.CreateFromSupplierPayment(
                tenantId,
                companyId,
                payment.BranchId,
                payment.SupplierId,
                company.CurrencyCode,
                payment.Id,
                payment.UnappliedAmount,
                originatorId
            );
        }

        await _supplierPayments.AddAsync(payment, ct);
        if (advance is not null)
            await _supplierCredits.AddAsync(advance, ct);

        try
        {
            // SUPPLIER-PAYMENTS-POSTING-15D: SaveChangesAsync publica SupplierPaymentConfirmedEvent
            // ANTES del commit (ErpDbContext.SaveChangesAsync, ADR-026 §8) —
            // SupplierPaymentConfirmedPostingTranslator lanza SupplierPaymentPostingFailedException
            // (nunca solo un warning) si el asiento no puede generarse. "No confirmar pago sin
            // asiento": el llamador revierte la transacción completa ante este fallo.
            await _supplierPayments.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            return Result<SupplierPaymentRegistration>.ValidationFailure(
                "Una de las cuentas por pagar afectadas fue modificada concurrentemente. Intente nuevamente."
            );
        }
        catch (SupplierPaymentPostingFailedException ex)
        {
            return Result<SupplierPaymentRegistration>.ValidationFailure(ex.Message, ex.Code);
        }

        return Result<SupplierPaymentRegistration>.Success(
            new SupplierPaymentRegistration(payment, advance?.Id)
        );
    }

    public async Task<string?> PrevalidateAsync(
        RegisterSupplierPaymentCommand intent,
        CancellationToken ct
    ) => (await CheckIntentAsync(intent, ct)).Error;

    private async Task<(string? Error, bool AllowWithoutPayable)> CheckIntentAsync(
        RegisterSupplierPaymentCommand cmd,
        CancellationToken ct
    )
    {
        // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — remanente sin confirmación explícita: rechazo
        // ANTES de cualquier efecto (sin transacción, sin secuencia, sin caja, sin asiento). El
        // dominio lo revalida de todas formas (defensa en profundidad).
        var unappliedAmount = cmd.TotalAmount - cmd.ApplicationLines.Sum(l => l.AmountApplied);
        if (unappliedAmount > 0 && !cmd.ConfirmUnappliedAmount)
            return (
                $"El pago supera el saldo que puede aplicarse en ${FormatMoney(unappliedAmount)}. Confirme que ese saldo quedará como anticipo a favor del proveedor.",
                false
            );

        // Política por empresa: solo gobierna el pago SIN ninguna CxP. Se resuelve únicamente
        // cuando aplica — el anticipo por sobrepago no depende de ella.
        if (cmd.ApplicationLines.Count > 0)
            return (null, false);
        var preferences = await _preferences.ResolveAsync(ct);
        var allowWithoutPayable = preferences.Payables?.AllowSupplierPaymentWithoutPayable ?? false;
        return allowWithoutPayable
            ? (null, true)
            : (
                "La empresa no permite registrar pagos a proveedores sin una cuenta por pagar: seleccione al menos una cuota.",
                false
            );
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
        // 02D-B — regla medio ↔ destino compartida con el reembolso de SupplierCredit.
        var destinationError = PaymentMethodDestinationPolicy.Validate(
            method,
            hasBankAccount: line.CompanyBankAccountId is not null,
            hasCashRegister: line.CashRegisterId is not null
        );
        if (destinationError is not null || method.AffectsPhysicalCash)
            return destinationError;

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
