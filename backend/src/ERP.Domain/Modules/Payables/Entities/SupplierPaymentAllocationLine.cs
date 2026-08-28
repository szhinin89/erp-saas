using ERP.Domain.Common;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — celda de la matriz medio↔cuota: cuánto de un
/// <see cref="SupplierPaymentMethodLine"/> puntual se destinó a una
/// <see cref="SupplierPaymentApplicationLine"/> puntual. Con 1 medio y 1 aplicación hay exactamente
/// una allocation; con N medios y M aplicaciones puede haber hasta N×M. <see cref="SupplierPayment.Create"/>
/// exige que cada medio quede distribuido al 100% y cada aplicación cubierta al 100% sumando estas
/// celdas.
/// </summary>
public sealed class SupplierPaymentAllocationLine : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierPaymentId { get; private set; }
    public Guid SupplierPaymentMethodLineId { get; private set; }
    public Guid SupplierPaymentApplicationLineId { get; private set; }
    public decimal Amount { get; private set; }

    private SupplierPaymentAllocationLine() { }

    /// <summary>Usar únicamente desde <see cref="SupplierPayment.Create"/>.</summary>
    internal static SupplierPaymentAllocationLine Create(
        Guid supplierPaymentId,
        Guid tenantId,
        Guid supplierPaymentMethodLineId,
        Guid supplierPaymentApplicationLineId,
        decimal amount
    )
    {
        if (supplierPaymentMethodLineId == Guid.Empty)
            throw new ArgumentException(
                "El medio de pago de la distribución es obligatorio.",
                nameof(supplierPaymentMethodLineId)
            );
        if (supplierPaymentApplicationLineId == Guid.Empty)
            throw new ArgumentException(
                "La aplicación de la distribución es obligatoria.",
                nameof(supplierPaymentApplicationLineId)
            );
        if (amount <= 0)
            throw new ArgumentException("El monto de la distribución debe ser mayor a cero.", nameof(amount));

        return new SupplierPaymentAllocationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierPaymentId = supplierPaymentId,
            SupplierPaymentMethodLineId = supplierPaymentMethodLineId,
            SupplierPaymentApplicationLineId = supplierPaymentApplicationLineId,
            Amount = amount,
        };
    }
}
