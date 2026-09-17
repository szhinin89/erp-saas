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
    public string? ReferenceNumber { get; private set; }
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
        string? notes
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
        };
    }
}
