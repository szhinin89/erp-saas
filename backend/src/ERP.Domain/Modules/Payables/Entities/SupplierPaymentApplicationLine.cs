using ERP.Domain.Common;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — aplicación de un <see cref="SupplierPayment"/> contra una
/// cuota puntual de <c>AccountsPayableInstallment</c> (única fuente viva de saldo de CxP). Entidad
/// hija sin repositorio propio. Una cuota puede recibir varios medios distintos — la distribución
/// medio↔cuota vive en <see cref="SupplierPaymentAllocationLine"/>, nunca aquí.
/// </summary>
public sealed class SupplierPaymentApplicationLine : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierPaymentId { get; private set; }
    public Guid AccountsPayableInstallmentId { get; private set; }
    public decimal AmountApplied { get; private set; }

    private SupplierPaymentApplicationLine() { }

    /// <summary>Usar únicamente desde <see cref="SupplierPayment.Create"/>.</summary>
    internal static SupplierPaymentApplicationLine Create(
        Guid supplierPaymentId,
        Guid tenantId,
        Guid accountsPayableInstallmentId,
        decimal amountApplied
    )
    {
        if (accountsPayableInstallmentId == Guid.Empty)
            throw new ArgumentException(
                "La cuota de la cuenta por pagar es obligatoria.",
                nameof(accountsPayableInstallmentId)
            );
        if (amountApplied <= 0)
            throw new ArgumentException(
                "El monto aplicado a la cuota debe ser mayor a cero.",
                nameof(amountApplied)
            );

        return new SupplierPaymentApplicationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierPaymentId = supplierPaymentId,
            AccountsPayableInstallmentId = accountsPayableInstallmentId,
            AmountApplied = amountApplied,
        };
    }
}
