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

    /// <summary>
    /// ACCOUNTING-JOURNAL-SOURCE-DOCUMENT-RESOLUTION-EXPENSES-PAYABLES-01 — proyección liviana
    /// para resolver el origen documental humano de un JournalEntry (número visible, proveedor,
    /// estado, fecha) — mismo criterio que
    /// <c>ISalesInvoiceRepository.GetJournalSourceSummariesByIdsAsync</c>. Con <c>companyId</c>
    /// explícito porque, a diferencia de <c>IExpenseDocumentRepository</c>, este repositorio no
    /// scopea por empresa activa vía <c>ForOperationalScope</c> (mismo motivo documentado en
    /// <see cref="ERP.Application.Modules.Accounting.Queries.IJournalEntrySourceModuleResolver"/>
    /// para <c>IPaymentRepository</c>).
    /// </summary>
    Task<
        IReadOnlyDictionary<
            Guid,
            (string DisplayNumber, Guid SupplierId, string Status, DateOnly PaymentDate)
        >
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    Task AddAsync(SupplierPayment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
