using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Interfaces;

public interface ISupplierPaymentRepository
{
    Task<SupplierPayment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// SUPPLIER-PAYMENTS-FRONTEND-15E — listado paginado para la pantalla de Pagos a Proveedores.
    /// Sin filtro por rango de fechas todavía (fuera de alcance de este ticket) — solo proveedor y
    /// estado, igual alcance mínimo que <c>PayablesController</c> necesitaba en su primera fase.
    /// </summary>
    Task<(IReadOnlyList<SupplierPayment> Items, int Total)> SearchAsync(
        Guid tenantId,
        Guid companyId,
        Guid? supplierId,
        SupplierPaymentStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

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
