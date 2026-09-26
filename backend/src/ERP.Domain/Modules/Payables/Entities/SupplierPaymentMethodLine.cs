using ERP.Domain.Common;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — un medio de pago usado dentro de un <see cref="SupplierPayment"/>.
/// Entidad hija sin repositorio propio (mismo criterio que <c>PaymentApplicationLine</c> frente a
/// <c>Payment</c>). Un pago puede tener varios medios; cada medio queda distribuido entre una o más
/// cuotas vía <see cref="SupplierPaymentAllocationLine"/> — este registro solo fija el monto total
/// tomado por ese medio, nunca contra qué cuota.
/// </summary>
public sealed class SupplierPaymentMethodLine : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierPaymentId { get; private set; }
    public Guid PaymentMethodId { get; private set; }

    /// <summary>Cuenta bancaria destino — exactamente uno de este campo o <see cref="CashRegisterId"/> es obligatorio (FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01).</summary>
    public Guid? CompanyBankAccountId { get; private set; }

    /// <summary>Caja destino — exactamente uno de este campo o <see cref="CompanyBankAccountId"/> es obligatorio.</summary>
    public Guid? CashRegisterId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>
    /// Número de operación/comprobante bancario (transferencia, depósito, voucher) — SSOT único del
    /// número de operación de la fuente; futura conciliación bancaria por
    /// Cuenta + <see cref="TransactionDate"/> + este campo + <see cref="Amount"/>.
    /// </summary>
    public string? ReferenceNumber { get; private set; }

    /// <summary>
    /// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — fecha efectiva real de la fuente bancaria
    /// (la que figura en el extracto del banco). Obligatoria y explícita para fuentes con
    /// <see cref="CompanyBankAccountId"/> — nunca se completa con <c>SupplierPayment.PaymentDate</c>
    /// (02A-FINAL); siempre <c>null</c> en fuentes de caja — el instante del efectivo lo
    /// fija el <c>CashMovement</c> vinculado. No sustituye a <see cref="CheckDate"/> (fecha girada
    /// en el cheque, puede ser posfechada).
    /// </summary>
    public DateOnly? TransactionDate { get; private set; }

    /// <summary>02A — sesión de caja donde salió el efectivo (solo fuentes de caja con medio que mueve efectivo físico).</summary>
    public Guid? CashSessionId { get; private set; }

    /// <summary>02A — egreso operativo registrado en la sesión (<c>CashMovementType.SupplierPayment</c>). Nunca postea.</summary>
    public Guid? CashMovementId { get; private set; }
    public string? CheckNumber { get; private set; }
    public DateOnly? CheckDate { get; private set; }
    public string? Notes { get; private set; }

    private SupplierPaymentMethodLine() { }

    /// <summary>Usar únicamente desde <see cref="SupplierPayment.Create"/>.</summary>
    internal static SupplierPaymentMethodLine Create(
        Guid supplierPaymentId,
        Guid tenantId,
        Guid paymentMethodId,
        Guid? companyBankAccountId,
        Guid? cashRegisterId,
        decimal amount,
        string? referenceNumber,
        string? checkNumber,
        DateOnly? checkDate,
        string? notes,
        DateOnly? transactionDate = null
    )
    {
        if (paymentMethodId == Guid.Empty)
            throw new ArgumentException("El medio de pago es obligatorio.", nameof(paymentMethodId));
        if (companyBankAccountId is null && cashRegisterId is null)
            throw new ArgumentException(
                "La caja o cuenta bancaria destino es obligatoria.",
                nameof(companyBankAccountId)
            );
        if (companyBankAccountId is not null && cashRegisterId is not null)
            throw new ArgumentException(
                "El medio de pago no puede tener cuenta bancaria y caja destino a la vez.",
                nameof(cashRegisterId)
            );
        if (amount <= 0)
            throw new ArgumentException("El monto del medio de pago debe ser mayor a cero.", nameof(amount));
        if (companyBankAccountId is not null && transactionDate is null)
            throw new ArgumentException(
                "La fecha de la transacción bancaria es obligatoria.",
                nameof(transactionDate)
            );
        if (cashRegisterId is not null && transactionDate is not null)
            throw new ArgumentException(
                "La fecha de transacción bancaria no aplica a un medio de pago en caja.",
                nameof(transactionDate)
            );

        return new SupplierPaymentMethodLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierPaymentId = supplierPaymentId,
            PaymentMethodId = paymentMethodId,
            CompanyBankAccountId = companyBankAccountId,
            CashRegisterId = cashRegisterId,
            Amount = amount,
            ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim(),
            CheckNumber = string.IsNullOrWhiteSpace(checkNumber) ? null : checkNumber.Trim(),
            CheckDate = checkDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            TransactionDate = transactionDate,
        };
    }

    /// <summary>Usar únicamente desde <see cref="SupplierPayment.LinkCashMovement"/>.</summary>
    internal void LinkCashMovement(Guid cashSessionId, Guid cashMovementId)
    {
        if (CashRegisterId is null)
            throw new InvalidOperationException(
                "Solo un medio de pago en caja puede vincularse a un movimiento de caja."
            );
        if (CashMovementId is not null)
            throw new InvalidOperationException(
                "El medio de pago ya está vinculado a un movimiento de caja."
            );
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("La sesión de caja es obligatoria.", nameof(cashSessionId));
        if (cashMovementId == Guid.Empty)
            throw new ArgumentException("El movimiento de caja es obligatorio.", nameof(cashMovementId));

        CashSessionId = cashSessionId;
        CashMovementId = cashMovementId;
    }
}
