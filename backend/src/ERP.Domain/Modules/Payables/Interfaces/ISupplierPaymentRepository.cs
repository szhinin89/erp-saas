using ERP.Domain.Modules.Payables.Entities;

namespace ERP.Domain.Modules.Payables.Interfaces;

public interface ISupplierPaymentRepository
{
    Task<SupplierPayment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// SUPPLIER-PAYMENTS-REGISTER-15C — respaldo de aplicación del índice único parcial
    /// <c>uq_supplier_payments_tenant_company_supplier_receipt_number</c>: permite devolver un error
    /// de validación legible antes de intentar el INSERT.
    /// </summary>
    Task<bool> ExistsByReceiptNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        string receiptNumber,
        CancellationToken ct = default
    );

    Task AddAsync(SupplierPayment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
